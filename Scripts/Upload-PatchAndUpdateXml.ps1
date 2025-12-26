# Script pour uploader le patch et update.xml à une release GitHub existante
# Usage: .\Upload-PatchAndUpdateXml.ps1 -Version "1.0.0.23" -GitHubToken "TOKEN"

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
Write-Host "   UPLOAD PATCH ET UPDATE.XML" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Récupérer la release existante
Write-Host "[1/4] Récupération de la release $TagName..." -ForegroundColor Yellow
try {
    $releaseUrl = "https://api.github.com/repos/$Repository/releases/tags/$TagName"
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
    Write-Host "  OK - Release trouvée" -ForegroundColor Green
    Write-Host "  URL: $($release.html_url)" -ForegroundColor Cyan
} catch {
    Write-Host "  ERREUR: Release introuvable pour le tag $TagName" -ForegroundColor Red
    Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Yellow
    exit 1
}

# Upload du patch
Write-Host "[2/4] Upload du patch..." -ForegroundColor Yellow
$patchPattern = "Amaliassistant_Patch_*_to_$Version.zip"
$patchDir = Join-Path $rootPath "Patches"
$patchFile = Get-ChildItem -Path $patchDir -Filter $patchPattern -ErrorAction SilentlyContinue | Select-Object -First 1

if ($patchFile) {
    # Supprimer l'ancien patch s'il existe
    $existingPatch = $release.assets | Where-Object { $_.name -like "*Patch*$Version*" }
    if ($existingPatch) {
        Write-Host "  Suppression de l'ancien patch..." -ForegroundColor Yellow
        foreach ($asset in $existingPatch) {
            try {
                $deleteUrl = "https://api.github.com/repos/$Repository/releases/assets/$($asset.id)"
                Invoke-RestMethod -Uri $deleteUrl -Method Delete -Headers $headers
                Write-Host "  OK - Ancien patch supprimé: $($asset.name)" -ForegroundColor Green
            } catch {
                Write-Host "  AVERTISSEMENT: Impossible de supprimer $($asset.name): $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
    }
    
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
        Write-Host "  URL: $($uploadResponse.browser_download_url)" -ForegroundColor Cyan
    } catch {
        Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  ATTENTION: Aucun patch trouvé pour la version $Version" -ForegroundColor Yellow
    Write-Host "  Pattern recherché: $patchPattern" -ForegroundColor White
}

Write-Host ""

# Upload de update.xml
Write-Host "[3/4] Upload de update.xml..." -ForegroundColor Yellow
$updateXmlPath = Join-Path $rootPath "update.xml"
if (Test-Path $updateXmlPath) {
    # Supprimer l'ancien update.xml s'il existe
    $existingUpdate = $release.assets | Where-Object { $_.name -eq "update.xml" }
    if ($existingUpdate) {
        Write-Host "  Suppression de l'ancien update.xml..." -ForegroundColor Yellow
        try {
            $deleteUrl = "https://api.github.com/repos/$Repository/releases/assets/$($existingUpdate.id)"
            Invoke-RestMethod -Uri $deleteUrl -Method Delete -Headers $headers
            Write-Host "  OK - Ancien update.xml supprimé" -ForegroundColor Green
        } catch {
            Write-Host "  AVERTISSEMENT: Impossible de supprimer l'ancien update.xml: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    try {
        $fileBytes = [System.IO.File]::ReadAllBytes($updateXmlPath)
        $fileContent = [System.Text.Encoding]::UTF8.GetString($fileBytes)
        
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
    }
} else {
    Write-Host "  ERREUR: update.xml introuvable" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   UPLOAD TERMINE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor White
Write-Host "Release: $($release.html_url)" -ForegroundColor Cyan
Write-Host ""



