# Script pour uploader un installateur sur une release GitHub existante
# Usage: .\Upload-InstallerToRelease.ps1 -Version "1.0.0.16" -InstallerPath "InstallerAppData\Amaliassistant_Setup.exe" -GitHubToken "TOKEN"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$InstallerPath,
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken
)

$ErrorActionPreference = "Stop"

$Repository = "lechieurdu0-sys/Amaliassistant"
$TagName = "v$Version"

$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept" = "application/vnd.github.v3+json"
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   UPLOAD INSTALLATEUR SUR GITHUB" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Vérifier que le fichier existe
$rootPath = Split-Path $PSScriptRoot -Parent
$fullInstallerPath = if ([System.IO.Path]::IsPathRooted($InstallerPath)) {
    $InstallerPath
} else {
    Join-Path $rootPath $InstallerPath
}

if (-not (Test-Path $fullInstallerPath)) {
    Write-Host "ERREUR: Le fichier installateur n'existe pas: $fullInstallerPath" -ForegroundColor Red
    exit 1
}

# Récupérer la release existante
Write-Host "[1/3] Récupération de la release $TagName..." -ForegroundColor Yellow
try {
    $releaseUrl = "https://api.github.com/repos/$Repository/releases/tags/$TagName"
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
    Write-Host "  OK - Release trouvée" -ForegroundColor Green
} catch {
    Write-Host "  ERREUR: Release introuvable pour le tag $TagName" -ForegroundColor Red
    Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Yellow
    exit 1
}

# Supprimer l'ancien installateur s'il existe
Write-Host "[2/3] Vérification des assets existants..." -ForegroundColor Yellow
$installerName = "Amaliassistant_Setup.exe"
$existingAsset = $release.assets | Where-Object { $_.name -eq $installerName }
if ($existingAsset) {
    Write-Host "  Ancien installateur trouvé, suppression..." -ForegroundColor Yellow
    try {
        $deleteUrl = "https://api.github.com/repos/$Repository/releases/assets/$($existingAsset.id)"
        Invoke-RestMethod -Uri $deleteUrl -Method Delete -Headers $headers
        Write-Host "  OK - Ancien installateur supprimé" -ForegroundColor Green
    } catch {
        Write-Host "  AVERTISSEMENT: Impossible de supprimer l'ancien installateur: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Aucun installateur existant" -ForegroundColor Green
}

# Upload du nouvel installateur
Write-Host "[3/3] Upload du nouvel installateur..." -ForegroundColor Yellow
try {
    $fileSize = (Get-Item $fullInstallerPath).Length
    $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
    Write-Host "  Fichier: $(Split-Path $fullInstallerPath -Leaf)" -ForegroundColor Cyan
    Write-Host "  Taille: $fileSizeMB MB" -ForegroundColor Cyan
    
    $uploadUrl = "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets?name=$installerName"
    $fileBytes = [System.IO.File]::ReadAllBytes($fullInstallerPath)
    
    Write-Host "  Upload en cours (cela peut prendre plusieurs minutes)..." -ForegroundColor Cyan
    $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/octet-stream" -TimeoutSec 1800
    Write-Host "  OK - Installateur uploadé avec succès" -ForegroundColor Green
    Write-Host "  URL: $($uploadResponse.browser_download_url)" -ForegroundColor Cyan
} catch {
    Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  Code HTTP: $statusCode" -ForegroundColor Yellow
    }
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   UPLOAD TERMINE AVEC SUCCES" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor White
Write-Host "Release: $($release.html_url)" -ForegroundColor Cyan
Write-Host ""

