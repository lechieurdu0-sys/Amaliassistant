# Script pour mettre à jour les notes d'une release GitHub existante
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken,
    
    [Parameter(Mandatory=$true)]
    [string]$ReleaseNotes,
    
    [Parameter(Mandatory=$false)]
    [string]$Repository = "lechieurdu0-sys/Amaliassistant"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   MISE A JOUR DES NOTES DE RELEASE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$tagName = "v$Version"
$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

Write-Host "[1/2] Récupération de la release..." -ForegroundColor Yellow
try {
    $releaseUrl = "https://api.github.com/repos/$Repository/releases/tags/$tagName"
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
    Write-Host "  OK - Release trouvée" -ForegroundColor Green
} catch {
    Write-Host "  ERREUR: Impossible de trouver la release $tagName" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Yellow
    exit 1
}

Write-Host "[2/2] Mise à jour des notes..." -ForegroundColor Yellow
$updateBody = @{
    body = $ReleaseNotes
} | ConvertTo-Json -Depth 10

try {
    $updateUrl = "https://api.github.com/repos/$Repository/releases/$($release.id)"
    $updatedRelease = Invoke-RestMethod -Uri $updateUrl -Method Patch -Headers $headers -Body $updateBody -ContentType "application/json"
    Write-Host "  OK - Notes mises à jour avec succès" -ForegroundColor Green
    Write-Host "  URL: $($updatedRelease.html_url)" -ForegroundColor Cyan
} catch {
    Write-Host "  ERREUR: Impossible de mettre à jour les notes" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   NOTES MISE A JOUR AVEC SUCCES" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""









