# Script pour créer une release GitHub avec notes de mise à jour et uploader les petits fichiers
# Usage: .\Create-Release.ps1 -Version "1.0.0.10" -GitHubToken "ghp_xxxxx" -ReleaseNotes "Corrections et améliorations"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken,
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "",
    
    [Parameter(Mandatory=$false)]
    [string]$Repository = "lechieurdu0-sys/Amaliassistant"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   CREATION RELEASE GITHUB" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Vérifier que la version est correcte
Write-Host "[1/5] Vérification de la version..." -ForegroundColor Yellow
$updateXmlPath = Join-Path $rootPath "update.xml"
if (Test-Path $updateXmlPath) {
    $updateXml = [xml](Get-Content $updateXmlPath)
    $currentVersion = $updateXml.item.version
    
    if ($currentVersion -ne $Version) {
        Write-Host "  ATTENTION: La version dans update.xml ($currentVersion) ne correspond pas à la version spécifiée ($Version)" -ForegroundColor Yellow
        $response = Read-Host "  Continuer quand même ? (O/N)"
        if ($response -ne "O" -and $response -ne "o") {
            Write-Host "  Annulé" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  OK - Version vérifiée: $Version" -ForegroundColor Green
    }
} else {
    Write-Host "  ATTENTION: update.xml introuvable" -ForegroundColor Yellow
}

Write-Host ""

# Préparer les notes de release
Write-Host "[2/5] Préparation des notes de release..." -ForegroundColor Yellow
if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    Write-Host "  Aucune note de release fournie, utilisation d'un template par défaut" -ForegroundColor Yellow
    $ReleaseNotes = @"
## Version $Version

### Corrections
- Corrections de bugs et améliorations de stabilité

### Améliorations
- Améliorations des performances
- Optimisations diverses

### Notes
- Cette mise à jour inclut des corrections et améliorations générales
"@
} else {
    Write-Host "  Notes de release fournies" -ForegroundColor Green
}

Write-Host ""
Write-Host "  Aperçu des notes:" -ForegroundColor Cyan
Write-Host $ReleaseNotes -ForegroundColor White
Write-Host ""

# Créer la release GitHub
Write-Host "[3/5] Création de la release GitHub..." -ForegroundColor Yellow
$tagName = "v$Version"
$releaseUrl = "https://api.github.com/repos/$Repository/releases"

$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$releaseBody = @{
    tag_name = $tagName
    name = "Version $Version"
    body = $ReleaseNotes
    draft = $false
    prerelease = $false
} | ConvertTo-Json -Depth 10 -Compress

try {
    Write-Host "  Création de la release avec le tag: $tagName" -ForegroundColor Cyan
    $release = Invoke-RestMethod -Uri $releaseUrl -Method Post -Headers $headers -Body $releaseBody -ContentType "application/json"
    Write-Host "  OK - Release créée avec succès" -ForegroundColor Green
    Write-Host "  URL: $($release.html_url)" -ForegroundColor Cyan
} catch {
    $statusCode = $null
    $responseBody = ""
    
    if ($_.Exception.Response) {
        try {
            $statusCode = $_.Exception.Response.StatusCode.value__
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $responseBody = $reader.ReadToEnd()
                $reader.Close()
                $stream.Close()
            }
        } catch {
            # Ignorer les erreurs de lecture du stream
        }
    }
    
    if ($statusCode) {
        Write-Host "  Code d'erreur HTTP: $statusCode" -ForegroundColor Yellow
    }
    
    if ($responseBody) {
        Write-Host "  Détails de l'erreur:" -ForegroundColor Yellow
        Write-Host $responseBody -ForegroundColor White
    } else {
        Write-Host "  Message d'erreur: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    if ($statusCode -eq 422) {
        Write-Host "  ERREUR: Le tag $tagName existe déjà" -ForegroundColor Red
        Write-Host "  Voulez-vous supprimer l'ancienne release et en créer une nouvelle ?" -ForegroundColor Yellow
        $response = Read-Host "  (O/N)"
        if ($response -eq "O" -or $response -eq "o") {
            # Récupérer l'ancienne release
            $existingReleaseUrl = "https://api.github.com/repos/$Repository/releases/tags/$tagName"
            try {
                $existingRelease = Invoke-RestMethod -Uri $existingReleaseUrl -Headers $headers
                Write-Host "  Suppression de l'ancienne release..." -ForegroundColor Yellow
                $deleteUrl = "https://api.github.com/repos/$Repository/releases/$($existingRelease.id)"
                Invoke-RestMethod -Uri $deleteUrl -Method Delete -Headers $headers
                Write-Host "  OK - Ancienne release supprimée" -ForegroundColor Green
                
                # Supprimer le tag
                Write-Host "  Suppression de l'ancien tag..." -ForegroundColor Yellow
                $deleteTagUrl = "https://api.github.com/repos/$Repository/git/refs/tags/$tagName"
                try {
                    Invoke-RestMethod -Uri $deleteTagUrl -Method Delete -Headers $headers
                    Write-Host "  OK - Ancien tag supprimé" -ForegroundColor Green
                } catch {
                    Write-Host "  ATTENTION: Impossible de supprimer le tag (peut être normal)" -ForegroundColor Yellow
                }
                
                # Recréer la release
                Write-Host "  Création de la nouvelle release..." -ForegroundColor Yellow
                $release = Invoke-RestMethod -Uri $releaseUrl -Method Post -Headers $headers -Body $releaseBody -ContentType "application/json"
                Write-Host "  OK - Release créée avec succès" -ForegroundColor Green
                Write-Host "  URL: $($release.html_url)" -ForegroundColor Cyan
            } catch {
                Write-Host "  ERREUR: Impossible de supprimer l'ancienne release: $($_.Exception.Message)" -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "  Annulé" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  ERREUR: Impossible de créer la release: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "  Détails: $responseBody" -ForegroundColor Yellow
        }
        exit 1
    }
}

Write-Host ""

# Upload du patch
Write-Host "[4/5] Upload du patch..." -ForegroundColor Yellow
$patchPattern = "Amaliassistant_Patch_*_to_$Version.zip"
$patchDir = Join-Path $rootPath "Patches"
$patchFile = Get-ChildItem -Path $patchDir -Filter $patchPattern -ErrorAction SilentlyContinue | Select-Object -First 1

$patchUploaded = $false
if ($patchFile) {
    try {
        $fileSize = (Get-Item $patchFile.FullName).Length
        $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
        Write-Host "  Fichier: $($patchFile.Name)" -ForegroundColor Cyan
        Write-Host "  Taille: $fileSizeMB MB" -ForegroundColor Cyan
        
        $uploadUrl = "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets?name=$($patchFile.Name)"
        $fileBytes = [System.IO.File]::ReadAllBytes($patchFile.FullName)
        
        Write-Host "  Upload en cours..." -ForegroundColor Cyan
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/zip" -TimeoutSec 300
        Write-Host "  OK - Patch uploadé avec succès" -ForegroundColor Green
        $patchUploaded = $true
    }
    catch {
        Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Le patch n'a pas été uploadé, mais la release a été créée" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Avertissement: Aucun patch trouvé pour la version $Version" -ForegroundColor Yellow
    Write-Host "  Pattern recherché: $patchPattern" -ForegroundColor White
}

Write-Host ""

# Upload de update.xml
Write-Host "[5/5] Upload de update.xml..." -ForegroundColor Yellow
$updateXmlPath = Join-Path $rootPath "update.xml"
if (Test-Path $updateXmlPath) {
    try {
        $fileSize = (Get-Item $updateXmlPath).Length
        Write-Host "  Taille: $fileSize octets" -ForegroundColor Cyan
        
        $uploadUrl = "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets?name=update.xml"
        $fileBytes = [System.IO.File]::ReadAllBytes($updateXmlPath)
        
        Write-Host "  Upload en cours..." -ForegroundColor Cyan
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/xml"
        Write-Host "  OK - update.xml uploadé avec succès" -ForegroundColor Green
    }
    catch {
        Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  update.xml n'a pas été uploadé, mais la release a été créée" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ERREUR: update.xml introuvable" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RELEASE CREE AVEC SUCCES" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Résumé:" -ForegroundColor Yellow
Write-Host "  Version: $Version" -ForegroundColor White
Write-Host "  Tag: $tagName" -ForegroundColor White
Write-Host "  URL: $($release.html_url)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Fichiers uploadés:" -ForegroundColor Yellow
if ($patchUploaded) {
    Write-Host "  OK - Patch: $($patchFile.Name)" -ForegroundColor Green
} else {
    Write-Host "  ERREUR - Patch: Non uploadé" -ForegroundColor Red
}
Write-Host "  OK - update.xml" -ForegroundColor Green
Write-Host ""
Write-Host "PROCHAINE ETAPE:" -ForegroundColor Yellow
Write-Host "  Uploadez manuellement l'installateur:" -ForegroundColor Cyan
Write-Host "  - InstallerAppData\Amaliassistant_Setup.exe (~164 MB)" -ForegroundColor White
Write-Host "  - Sur la page: $($release.html_url)" -ForegroundColor White
Write-Host ""

