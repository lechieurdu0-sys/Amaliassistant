# Script pour uploader update.xml sur une release GitHub existante
# Usage: .\Upload-UpdateXml.ps1 -Version "1.0.0.23" -GitHubToken "TOKEN"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken
)

$ErrorActionPreference = "Stop"

$Repository = "lechieurdu0-sys/Amaliassistant"
$TagName = "v$Version"

$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   UPLOAD UPDATE.XML SUR GITHUB" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Vérifier que le fichier existe
$rootPath = Split-Path $PSScriptRoot -Parent
$updateXmlPath = Join-Path $rootPath "update.xml"

if (-not (Test-Path $updateXmlPath)) {
    Write-Host "ERREUR: Le fichier update.xml n'existe pas: $updateXmlPath" -ForegroundColor Red
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

# Supprimer l'ancien update.xml s'il existe
Write-Host "[2/3] Vérification des assets existants..." -ForegroundColor Yellow
$existingAsset = $release.assets | Where-Object { $_.name -eq "update.xml" }
if ($existingAsset) {
    Write-Host "  Ancien update.xml trouvé, suppression..." -ForegroundColor Yellow
    try {
        $deleteUrl = "https://api.github.com/repos/$Repository/releases/assets/$($existingAsset.id)"
        Invoke-RestMethod -Uri $deleteUrl -Method Delete -Headers $headers
        Write-Host "  OK - Ancien update.xml supprimé" -ForegroundColor Green
    } catch {
        Write-Host "  AVERTISSEMENT: Impossible de supprimer l'ancien update.xml: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Aucun update.xml existant" -ForegroundColor Green
}

# Upload du nouvel update.xml
Write-Host "[3/3] Upload du nouvel update.xml..." -ForegroundColor Yellow
try {
    $fileBytes = [System.IO.File]::ReadAllBytes($updateXmlPath)
    $fileContent = [System.Text.Encoding]::UTF8.GetString($fileBytes)
    
    # Afficher le contenu pour vérification
    Write-Host "  Contenu du fichier:" -ForegroundColor Cyan
    $fileContent | Select-String -Pattern "<version>" | ForEach-Object { Write-Host "    $_" -ForegroundColor White }
    
    $uploadUrl = "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets?name=update.xml"
    
    Write-Host "  Upload en cours..." -ForegroundColor Cyan
    $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/xml"
    Write-Host "  OK - update.xml uploadé avec succès" -ForegroundColor Green
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







