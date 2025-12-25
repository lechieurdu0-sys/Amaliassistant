# Script simplifié pour créer la release GitHub 1.0.0.11
param(
    [string]$GitHubToken = ""
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
Write-Host "   CREATION RELEASE GITHUB 1.0.0.11" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$rootPath = "D:\Users\lechi\Desktop\Amaliassistant 2.0"
$version = "1.0.0.11"
$tagName = "v$version"
$repository = "lechieurdu0-sys/Amaliassistant"

$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

# Notes de release
$releaseNotes = @"
## Version 1.0.0.11

### Améliorations
- Optimisation du système de mise à jour
- MessageBox de mise à jour affiché rapidement (< 1 seconde)
- Mise à jour silencieuse après confirmation utilisateur
- Redémarrage automatique après la mise à jour

### Optimisations
- Délai initial de vérification réduit : 3s → 0.5s
- Timeout pour update.xml : 5s (au lieu de 5 minutes)
- Suppression des Dispatcher.Invoke inutiles
- Mode silencieux pour l'installateur complet

### Technique
- Amélioration de la réactivité du système de mise à jour
- Gestion optimisée des timeouts réseau
- Processus de mise à jour non-bloquant
"@

# Vérifier si le tag existe déjà
Write-Host "[1/4] Vérification du tag..." -ForegroundColor Yellow
try {
    $tagUrl = "https://api.github.com/repos/$repository/git/refs/tags/$tagName"
    $existingTag = Invoke-RestMethod -Uri $tagUrl -Headers $headers
    Write-Host "  ATTENTION: Le tag $tagName existe déjà" -ForegroundColor Yellow
    Write-Host "  Suppression du tag existant..." -ForegroundColor Yellow
    Invoke-RestMethod -Uri $tagUrl -Method Delete -Headers $headers
    Write-Host "  OK - Tag supprimé" -ForegroundColor Green
} catch {
    Write-Host "  OK - Tag n'existe pas encore" -ForegroundColor Green
}

# Vérifier si une release existe déjà
Write-Host "[2/4] Vérification de la release..." -ForegroundColor Yellow
try {
    $releaseUrl = "https://api.github.com/repos/$repository/releases/tags/$tagName"
    $existingRelease = Invoke-RestMethod -Uri $releaseUrl -Headers $headers
    Write-Host "  ATTENTION: Une release existe déjà pour ce tag" -ForegroundColor Yellow
    Write-Host "  Suppression de l'ancienne release..." -ForegroundColor Yellow
    $deleteUrl = "https://api.github.com/repos/$repository/releases/$($existingRelease.id)"
    Invoke-RestMethod -Uri $deleteUrl -Method Delete -Headers $headers
    Write-Host "  OK - Release supprimée" -ForegroundColor Green
} catch {
    Write-Host "  OK - Aucune release existante" -ForegroundColor Green
}

# Créer la release
Write-Host "[3/4] Création de la release..." -ForegroundColor Yellow
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
    }
    exit 1
}

# Upload du patch
Write-Host "[4/4] Upload des fichiers..." -ForegroundColor Yellow
$patchFile = Join-Path $rootPath "Patches\Amaliassistant_Patch_1.0.0.10_to_1.0.0.11.zip"
if (-not (Test-Path $patchFile)) {
    # Chercher le fichier avec un pattern différent
    $patchPattern = "*_to_$version.zip"
    $patchDir = Join-Path $rootPath "Patches"
    $foundPatch = Get-ChildItem -Path $patchDir -Filter $patchPattern -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($foundPatch) {
        $patchFile = $foundPatch.FullName
    }
}

if (Test-Path $patchFile) {
    Write-Host "  Upload du patch: $(Split-Path $patchFile -Leaf)" -ForegroundColor Cyan
    $fileBytes = [System.IO.File]::ReadAllBytes($patchFile)
    $uploadUrl = "https://uploads.github.com/repos/$repository/releases/$($release.id)/assets?name=$(Split-Path $patchFile -Leaf)"
    try {
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/zip" -TimeoutSec 300
        Write-Host "  OK - Patch uploadé" -ForegroundColor Green
    } catch {
        Write-Host "  ERREUR lors de l'upload du patch: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  ATTENTION: Fichier patch introuvable" -ForegroundColor Yellow
}

# Upload de update.xml
$updateXmlPath = Join-Path $rootPath "update.xml"
if (Test-Path $updateXmlPath) {
    Write-Host "  Upload de update.xml" -ForegroundColor Cyan
    $fileBytes = [System.IO.File]::ReadAllBytes($updateXmlPath)
    $uploadUrl = "https://uploads.github.com/repos/$repository/releases/$($release.id)/assets?name=update.xml"
    try {
        $uploadResponse = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $headers -Body $fileBytes -ContentType "application/xml"
        Write-Host "  OK - update.xml uploadé" -ForegroundColor Green
    } catch {
        Write-Host "  ERREUR lors de l'upload de update.xml: $($_.Exception.Message)" -ForegroundColor Red
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







