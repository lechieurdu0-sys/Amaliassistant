# Script pour créer la release étape par étape
param(
    [string]$GitHubToken = "",
    [string]$ReleaseNotes = ""
)

# Si aucun token fourni, utiliser Get-GitHubToken.ps1
if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    $GitHubToken = & "$PSScriptRoot\Get-GitHubToken.ps1" -RequireToken
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
        Write-Host "ERREUR: Token GitHub requis" -ForegroundColor Red
        exit 1
    }
}

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   CREATION RELEASE GITHUB" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$rootPath = "D:\Users\lechi\Desktop\Amaliassistant 2.0"

# Lire la version depuis update.xml
$updateXmlPath = Join-Path $rootPath "update.xml"
$version = "1.0.0.12"
if (Test-Path $updateXmlPath) {
    try {
        $updateXml = [xml](Get-Content $updateXmlPath)
        $version = $updateXml.item.version
    } catch {
        # Utiliser la version par défaut
    }
}

$tagName = "v$version"
$repository = "lechieurdu0-sys/Amaliassistant"

$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

# Notes de release (utiliser le paramètre si fourni, sinon notes par défaut)
if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    # Notes par défaut si aucune note n'est fournie
    $releaseNotes = "## Version $version`n`n### Ameliorations`n- Corrections et ameliorations generales`n`n### Technique`n- Mise a jour de l'application"
} else {
    $releaseNotes = $ReleaseNotes
}

# Étape 1: Récupérer le SHA du commit actuel
Write-Host "[1/5] Récupération du SHA du commit..." -ForegroundColor Yellow
try {
    $commitsUrl = "https://api.github.com/repos/$repository/commits/main"
    $commit = Invoke-RestMethod -Uri $commitsUrl -Headers $headers
    $sha = $commit.sha
    Write-Host "  OK - SHA: $sha" -ForegroundColor Green
} catch {
    Write-Host "  ERREUR: Impossible de récupérer le SHA" -ForegroundColor Red
    exit 1
}

# Étape 2: Créer le tag
Write-Host "[2/5] Création du tag..." -ForegroundColor Yellow
try {
    $tagBody = @{
        ref = "refs/tags/$tagName"
        sha = $sha
    } | ConvertTo-Json
    
    $tagUrl = "https://api.github.com/repos/$repository/git/refs"
    $tag = Invoke-RestMethod -Uri $tagUrl -Method Post -Headers $headers -Body $tagBody -ContentType "application/json"
    Write-Host "  OK - Tag créé" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 422) {
        Write-Host "  ATTENTION: Le tag existe déjà, suppression..." -ForegroundColor Yellow
        try {
            $deleteTagUrl = "https://api.github.com/repos/$repository/git/refs/tags/$tagName"
            Invoke-RestMethod -Uri $deleteTagUrl -Method Delete -Headers $headers
            Write-Host "  OK - Tag supprimé, recréation..." -ForegroundColor Green
            $tag = Invoke-RestMethod -Uri $tagUrl -Method Post -Headers $headers -Body $tagBody -ContentType "application/json"
            Write-Host "  OK - Tag créé" -ForegroundColor Green
        } catch {
            Write-Host "  ERREUR: Impossible de créer le tag" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Étape 3: Créer la release
Write-Host "[3/5] Création de la release..." -ForegroundColor Yellow
$releaseBody = @{
    tag_name = $tagName
    name = "Version $version"
    body = $releaseNotes
    draft = $false
    prerelease = $false
} | ConvertTo-Json -Depth 10

try {
    $createUrl = "https://api.github.com/repos/$repository/releases"
    $release = Invoke-RestMethod -Uri $createUrl -Method Post -Headers $headers -Body $releaseBody -ContentType "application/json"
    Write-Host "  OK - Release créée avec succès" -ForegroundColor Green
    Write-Host "  URL: $($release.html_url)" -ForegroundColor Cyan
} catch {
    Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  Code HTTP: $statusCode" -ForegroundColor Yellow
        
        # Essayer de lire le message d'erreur
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $errorBody = $reader.ReadToEnd()
            $reader.Close()
            $stream.Close()
            Write-Host "  Détails: $errorBody" -ForegroundColor White
        } catch {
            # Ignorer
        }
    }
    exit 1
}

# Étape 4: Upload du patch
Write-Host "[4/5] Upload du patch..." -ForegroundColor Yellow

# Chercher le patch qui correspond à cette version (format: *_to_$version.zip)
$patchPattern = "*_to_$version.zip"
$patchDir = Join-Path $rootPath "Patches"
$patchFile = $null

# Chercher le patch avec le pattern correct
$foundPatches = Get-ChildItem -Path $patchDir -Filter $patchPattern -ErrorAction SilentlyContinue
if ($foundPatches) {
    # Prendre le plus récent si plusieurs correspondent
    $foundPatch = $foundPatches | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $patchFile = $foundPatch.FullName
    Write-Host "  Patch trouvé: $(Split-Path $patchFile -Leaf)" -ForegroundColor Cyan
    
    # Vérifier que le nom est logique (version précédente < version actuelle)
    $patchName = Split-Path $patchFile -Leaf
    if ($patchName -match 'Amaliassistant_Patch_(\d+\.\d+\.\d+\.\d+)_to_(\d+\.\d+\.\d+\.\d+)\.zip') {
        $patchFromVersion = $matches[1]
        $patchToVersion = $matches[2]
        if ($patchToVersion -ne $version) {
            Write-Host "  ATTENTION: Le patch trouvé ($patchName) ne correspond pas à la version $version" -ForegroundColor Yellow
            $patchFile = $null
        } else {
            Write-Host "  Patch valide: de $patchFromVersion vers $patchToVersion" -ForegroundColor Green
        }
    }
}

if (Test-Path $patchFile) {
    Write-Host "  Upload: $(Split-Path $patchFile -Leaf)" -ForegroundColor Cyan
    $fileBytes = [System.IO.File]::ReadAllBytes($patchFile)
    $uploadUrl = "https://uploads.github.com/repos/$repository/releases/$($release.id)/assets?name=$(Split-Path $patchFile -Leaf)"
    try {
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/zip" -TimeoutSec 300
        Write-Host "  OK - Patch uploadé" -ForegroundColor Green
    } catch {
        Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  ATTENTION: Fichier patch introuvable" -ForegroundColor Yellow
}

# Étape 5: Upload de update.xml
Write-Host "[5/5] Upload de update.xml..." -ForegroundColor Yellow
$updateXmlPath = Join-Path $rootPath "update.xml"
if (Test-Path $updateXmlPath) {
    $fileBytes = [System.IO.File]::ReadAllBytes($updateXmlPath)
    $uploadUrl = "https://uploads.github.com/repos/$repository/releases/$($release.id)/assets?name=update.xml"
    try {
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/xml"
        Write-Host "  OK - update.xml uploadé" -ForegroundColor Green
    } catch {
        Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  ERREUR: update.xml introuvable" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RELEASE CREE AVEC SUCCES" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "URL: $($release.html_url)" -ForegroundColor Cyan
Write-Host ""
Write-Host "PROCHAINE ETAPE:" -ForegroundColor Yellow
Write-Host "  Uploadez manuellement l'installateur:" -ForegroundColor Cyan
Write-Host "  - InstallerAppData\Amaliassistant_Setup.exe (~164 MB)" -ForegroundColor White
Write-Host ""

