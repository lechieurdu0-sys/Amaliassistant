# Script pour créer une release GitHub dédiée aux plugins
# Usage: .\Create-PluginRelease.ps1 -Version "1.0.0" -GitHubToken "ghp_xxxxx" -PluginFiles @("DigitalClockPlugin.dll")
# Usage: .\Create-PluginRelease.ps1 -Version "1.0.0" -PluginFiles @("DigitalClockPlugin.dll")

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubToken = "",
    
    [Parameter(Mandatory=$false)]
    [string[]]$PluginFiles = @(),
    
    [Parameter(Mandatory=$false)]
    [string]$Repository = "lechieurdu0-sys/Amaliassistant",
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "",
    
    [Parameter(Mandatory=$false)]
    [string]$PluginDirectory = "$PSScriptRoot\..\Plugins"
)

$ErrorActionPreference = "Stop"

# Charger le token GitHub
if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    # Méthode 1: Variable d'environnement
    $GitHubToken = $env:GITHUB_TOKEN
    
    # Méthode 2: Fichier TokenGitHub.txt
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
        $rootPath = Split-Path $PSScriptRoot -Parent
        $tokenFile = Join-Path $rootPath "TokenGitHub.txt"
        if (Test-Path $tokenFile) {
            try {
                $GitHubToken = (Get-Content $tokenFile -Raw).Trim()
            } catch {
                # Ignorer l'erreur
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    Write-Host "ERREUR: Aucun token GitHub fourni" -ForegroundColor Red
    Write-Host "Utilisez -GitHubToken ou configurez GITHUB_TOKEN ou TokenGitHub.txt" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   CREATION RELEASE PLUGINS GITHUB" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$tagName = "plugins-v$Version"
$releaseUrl = "https://api.github.com/repos/$Repository/releases"

$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

# Préparer les notes de release
Write-Host "[1/4] Préparation des notes de release..." -ForegroundColor Yellow
if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $ReleaseNotes = @"
## Plugins Version $Version

Cette release contient les plugins disponibles pour Amaliassistant.

### Plugins inclus
$($PluginFiles | ForEach-Object { "- $_" } | Out-String)
"@
} else {
    Write-Host "  Notes de release fournies" -ForegroundColor Green
}

Write-Host "  Aperçu des notes:" -ForegroundColor Cyan
Write-Host $ReleaseNotes -ForegroundColor White
Write-Host ""

# Vérifier les fichiers de plugins
Write-Host "[2/4] Vérification des fichiers de plugins..." -ForegroundColor Yellow
$pluginFilesToUpload = @()

if ($PluginFiles.Count -eq 0) {
    # Chercher automatiquement tous les plugins dans le répertoire
    Write-Host "  Aucun fichier spécifié, recherche automatique..." -ForegroundColor Cyan
    
    # Chercher dans le répertoire des plugins compilés
    $pluginBuildDir = "$PluginDirectory\*\bin\*\*\*.dll"
    $foundPlugins = Get-ChildItem -Path $pluginBuildDir -Filter "*.dll" -ErrorAction SilentlyContinue
    
    if ($foundPlugins.Count -eq 0) {
        # Chercher directement dans le répertoire AppData/Plugins
        $appDataPluginsDir = "$env:APPDATA\Amaliassistant\Plugins"
        if (Test-Path $appDataPluginsDir) {
            $foundPlugins = Get-ChildItem -Path $appDataPluginsDir -Filter "*.dll" -ErrorAction SilentlyContinue
        }
    }
    
    if ($foundPlugins.Count -eq 0) {
        Write-Host "  ERREUR: Aucun plugin trouvé" -ForegroundColor Red
        Write-Host "  Spécifiez les fichiers avec -PluginFiles ou placez les DLL dans le répertoire des plugins" -ForegroundColor Yellow
        exit 1
    }
    
    $PluginFiles = $foundPlugins | ForEach-Object { $_.Name }
    Write-Host "  $($PluginFiles.Count) plugin(s) trouvé(s) automatiquement" -ForegroundColor Green
}

foreach ($pluginFile in $PluginFiles) {
    $fullPath = $null
    
    # Chercher le fichier dans plusieurs emplacements
    $searchPaths = @(
        $pluginFile,  # Chemin absolu ou relatif fourni
        "$PluginDirectory\*\bin\*\*\$pluginFile",  # Répertoire de build
        "$env:APPDATA\Amaliassistant\Plugins\$pluginFile",  # AppData
        "$PSScriptRoot\..\Plugins\$pluginFile"  # Racine plugins
    )
    
    foreach ($path in $searchPaths) {
        if (Test-Path $path) {
            $fullPath = (Resolve-Path $path).Path
            break
        }
    }
    
    if ($null -eq $fullPath) {
        Write-Host "  ERREUR: Fichier introuvable: $pluginFile" -ForegroundColor Red
        exit 1
    }
    
    $fileInfo = Get-Item $fullPath
    Write-Host "  ✓ $($fileInfo.Name) ($([math]::Round($fileInfo.Length / 1KB, 2)) KB)" -ForegroundColor Green
    $pluginFilesToUpload += @{
        Path = $fullPath
        Name = $fileInfo.Name
    }
}

Write-Host ""

# Créer la release GitHub
Write-Host "[3/4] Création de la release GitHub..." -ForegroundColor Yellow

$releaseBody = @{
    tag_name = $tagName
    name = "Plugins v$Version"
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
    
    if ($statusCode -eq 422 -and $responseBody -like "*already_exists*") {
        Write-Host "  Le tag $tagName existe déjà, récupération de la release existante..." -ForegroundColor Yellow
        try {
            $existingReleaseUrl = "https://api.github.com/repos/$Repository/releases/tags/$tagName"
            $release = Invoke-RestMethod -Uri $existingReleaseUrl -Headers $headers
            Write-Host "  OK - Release existante trouvée" -ForegroundColor Green
        } catch {
            Write-Host "  ERREUR: Impossible de récupérer la release existante" -ForegroundColor Red
            Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "  ERREUR: Impossible de créer la release" -ForegroundColor Red
        if ($statusCode) {
            Write-Host "  Code HTTP: $statusCode" -ForegroundColor Yellow
        }
        if ($responseBody) {
            Write-Host "  Détails: $responseBody" -ForegroundColor Yellow
        }
        exit 1
    }
}

Write-Host ""

# Upload des fichiers de plugins
Write-Host "[4/4] Upload des fichiers de plugins..." -ForegroundColor Yellow

foreach ($plugin in $pluginFilesToUpload) {
    Write-Host "  Upload de $($plugin.Name)..." -ForegroundColor Cyan
    
    # Supprimer l'ancien asset s'il existe
    $existingAsset = $release.assets | Where-Object { $_.name -eq $plugin.Name }
    if ($existingAsset) {
        Write-Host "    Suppression de l'ancienne version..." -ForegroundColor Yellow
        try {
            $deleteUrl = "https://api.github.com/repos/$Repository/releases/assets/$($existingAsset.id)"
            Invoke-RestMethod -Uri $deleteUrl -Method Delete -Headers $headers
            Write-Host "    OK - Ancienne version supprimée" -ForegroundColor Green
        } catch {
            Write-Host "    AVERTISSEMENT: Impossible de supprimer l'ancienne version: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    try {
        $fileBytes = [System.IO.File]::ReadAllBytes($plugin.Path)
        $uploadUrl = "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets?name=$($plugin.Name)"
        
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/octet-stream"
        Write-Host "    ✓ Uploadé avec succès" -ForegroundColor Green
        Write-Host "    URL: $($uploadResponse.browser_download_url)" -ForegroundColor Cyan
    } catch {
        Write-Host "    ✗ ERREUR: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode.value__
            Write-Host "    Code HTTP: $statusCode" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   RELEASE PLUGINS CREE AVEC SUCCES" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Release: $($release.html_url)" -ForegroundColor Cyan
Write-Host "Tag: $tagName" -ForegroundColor Cyan
Write-Host ""
Write-Host "Les URLs de téléchargement suivent le format:" -ForegroundColor Yellow
Write-Host "https://github.com/$Repository/releases/download/$tagName/{PluginName}.dll" -ForegroundColor White
Write-Host ""

