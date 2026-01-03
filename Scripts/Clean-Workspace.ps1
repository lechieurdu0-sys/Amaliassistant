# Script de nettoyage prudent de l'espace de travail
# Supprime uniquement les artefacts de build et fichiers temporaires
# NE SUPPRIME PAS les fichiers source, scripts, configs, etc.

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   NETTOYAGE DE L'ESPACE DE TRAVAIL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Dossiers bin/ et obj/ à supprimer (artefacts de build)
$buildArtifacts = @(
    "GameOverlay.App\bin",
    "GameOverlay.App\obj",
    "GameOverlay.Kikimeter\bin",
    "GameOverlay.Kikimeter\obj",
    "GameOverlay.Windows\bin",
    "GameOverlay.Windows\obj",
    "GameOverlay.Models\bin",
    "GameOverlay.Models\obj",
    "GameOverlay.Themes\bin",
    "GameOverlay.Themes\obj",
    "GameOverlay.XpTracker\bin",
    "GameOverlay.XpTracker\obj",
    "GameOverlay.UpdateInstaller\bin",
    "GameOverlay.UpdateInstaller\obj"
)

# Dossiers temporaires (peuvent être régénérés)
$tempDirs = @(
    "publish",           # Peut être régénéré avec Build-Release.ps1
    "publish_old"        # Sauvegarde pour patches, peut être régénéré
)

# Fichiers temporaires et de cache
$tempFiles = @(
    "*.user",
    "*.suo",
    "*.cache",
    ".vs",
    ".vscode\settings.json"  # Seulement settings.json, pas tout .vscode
)

Write-Host "[1/3] Suppression des artefacts de build (bin/ et obj/)..." -ForegroundColor Yellow
$deletedCount = 0
foreach ($artifact in $buildArtifacts) {
    $fullPath = Join-Path $rootPath $artifact
    if (Test-Path $fullPath) {
        try {
            Remove-Item -Path $fullPath -Recurse -Force -ErrorAction Stop
            Write-Host "  ✓ Supprimé: $artifact" -ForegroundColor Green
            $deletedCount++
        } catch {
            Write-Host "  ✗ Erreur lors de la suppression de $artifact : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}
if ($deletedCount -eq 0) {
    Write-Host "  Aucun artefact de build trouvé" -ForegroundColor Gray
}
Write-Host ""

Write-Host "[2/3] Suppression des dossiers temporaires..." -ForegroundColor Yellow
$deletedCount = 0
foreach ($tempDir in $tempDirs) {
    $fullPath = Join-Path $rootPath $tempDir
    if (Test-Path $fullPath) {
        try {
            Remove-Item -Path $fullPath -Recurse -Force -ErrorAction Stop
            Write-Host "  ✓ Supprimé: $tempDir" -ForegroundColor Green
            $deletedCount++
        } catch {
            Write-Host "  ✗ Erreur lors de la suppression de $tempDir : $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "    (Peut être verrouillé par un processus)" -ForegroundColor Yellow
        }
    }
}
if ($deletedCount -eq 0) {
    Write-Host "  Aucun dossier temporaire trouvé" -ForegroundColor Gray
}
Write-Host ""

Write-Host "[3/3] Suppression des fichiers temporaires..." -ForegroundColor Yellow
$deletedCount = 0

# Supprimer les fichiers .user et .suo
Get-ChildItem -Path $rootPath -Filter "*.user" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        Remove-Item -Path $_.FullName -Force -ErrorAction Stop
        Write-Host "  ✓ Supprimé: $($_.Name)" -ForegroundColor Green
        $deletedCount++
    } catch {
        Write-Host "  ✗ Erreur: $($_.Name)" -ForegroundColor Red
    }
}

Get-ChildItem -Path $rootPath -Filter "*.suo" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        Remove-Item -Path $_.FullName -Force -ErrorAction Stop
        Write-Host "  ✓ Supprimé: $($_.Name)" -ForegroundColor Green
        $deletedCount++
    } catch {
        Write-Host "  ✗ Erreur: $($_.Name)" -ForegroundColor Red
    }
}

# Supprimer le dossier .vs s'il existe (cache Visual Studio)
$vsPath = Join-Path $rootPath ".vs"
if (Test-Path $vsPath) {
    try {
        Remove-Item -Path $vsPath -Recurse -Force -ErrorAction Stop
        Write-Host "  ✓ Supprimé: .vs" -ForegroundColor Green
        $deletedCount++
    } catch {
        Write-Host "  ✗ Erreur lors de la suppression de .vs" -ForegroundColor Red
    }
}

if ($deletedCount -eq 0) {
    Write-Host "  Aucun fichier temporaire trouvé" -ForegroundColor Gray
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   NETTOYAGE TERMINE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Fichiers CONSERVES (non supprimés) :" -ForegroundColor Yellow
Write-Host "  ✓ Code source (.cs, .xaml, .csproj)" -ForegroundColor Green
Write-Host "  ✓ Scripts PowerShell (.ps1, .bat)" -ForegroundColor Green
Write-Host "  ✓ Documentation (Docs/)" -ForegroundColor Green
Write-Host "  ✓ Configuration (update.xml, installer.iss)" -ForegroundColor Green
Write-Host "  ✓ Ressources (images, sons, etc.)" -ForegroundColor Green
Write-Host "  ✓ Patches/ (archives de mise à jour)" -ForegroundColor Green
Write-Host "  ✓ InstallerAppData/ (installateurs créés)" -ForegroundColor Green
Write-Host "  ✓ Prerequisites/ (prérequis pour l'installateur)" -ForegroundColor Green
Write-Host ""

























