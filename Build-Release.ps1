# Script de Release pour Amaliassistant
# Nettoie, build et publie l'application en mode Release

param(
    [switch]$Clean,
    [switch]$SkipClean,
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"
$RootPath = $PSScriptRoot
$AppProject = Join-Path $RootPath "GameOverlay.App\GameOverlay.App.csproj"
$PublishDir = Join-Path $RootPath $OutputDir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   BUILD RELEASE - Amaliassistant" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Vérifier que le projet existe
if (-not (Test-Path $AppProject)) {
    Write-Host "ERREUR: Projet introuvable: $AppProject" -ForegroundColor Red
    exit 1
}

# Étape 1: Nettoyage
if ($Clean -or -not $SkipClean) {
    Write-Host "[1/4] Nettoyage des anciens builds..." -ForegroundColor Yellow
    
    # Supprimer les dossiers bin et obj
    $projects = @(
        "GameOverlay.App",
        "GameOverlay.Kikimeter",
        "GameOverlay.Windows",
        "GameOverlay.Models",
        "GameOverlay.Themes",
        "GameOverlay.XpTracker"
    )
    
    foreach ($proj in $projects) {
        $binPath = Join-Path $RootPath "$proj\bin"
        $objPath = Join-Path $RootPath "$proj\obj"
        
        if (Test-Path $binPath) {
            Remove-Item -Path $binPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  Supprimé: $binPath" -ForegroundColor Gray
        }
        
        if (Test-Path $objPath) {
            Remove-Item -Path $objPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  Supprimé: $objPath" -ForegroundColor Gray
        }
    }
    
    # Supprimer le dossier publish existant si demandé
    if ($Clean -and (Test-Path $PublishDir)) {
        Remove-Item -Path $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Supprimé: $PublishDir" -ForegroundColor Gray
    }
    
    Write-Host "  OK - Nettoyage terminé" -ForegroundColor Green
    Write-Host ""
}

# Étape 2: Restauration des packages
Write-Host "[2/4] Restauration des packages NuGet..." -ForegroundColor Yellow
try {
    dotnet restore "$AppProject" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Échec de la restauration"
    }
    Write-Host "  OK - Packages restaurés" -ForegroundColor Green
} catch {
    Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Étape 3: Publication en Release
Write-Host "[3/4] Publication de l'application (Release)..." -ForegroundColor Yellow

# Créer le dossier de sortie
if (-not (Test-Path $PublishDir)) {
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
}

$publishArgs = @(
    "publish",
    "`"$AppProject`"",
    "-c", "Release",
    "-o", "`"$PublishDir`"",
    "--self-contained", "false",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "/p:PublishReadyToRun=true",
    "/p:TrimUnusedDependencies=false"
)

Write-Host "  Commande: dotnet $($publishArgs -join ' ')" -ForegroundColor Gray

try {
    & dotnet $publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Échec de la publication"
    }
    Write-Host "  OK - Publication réussie" -ForegroundColor Green
} catch {
    Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Étape 4: Nettoyage du dossier publish
Write-Host "[4/4] Nettoyage des fichiers inutiles..." -ForegroundColor Yellow

# Supprimer les fichiers .pdb
Get-ChildItem -Path $PublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
Write-Host "  Supprimé: fichiers .pdb" -ForegroundColor Gray

# Supprimer les fichiers obsolètes
$obsoleteFiles = @(
    "GameOverlay.Video.*",
    "GameOverlay.ZQSD.*",
    "GameOverlay.ServerSessions.*"
)

foreach ($pattern in $obsoleteFiles) {
    Get-ChildItem -Path $PublishDir -Filter $pattern -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

# Supprimer les dossiers inutiles
$foldersToDelete = @("logs", "Release")
foreach ($folder in $foldersToDelete) {
    $folderPath = Join-Path $PublishDir $folder
    if (Test-Path $folderPath) {
        Remove-Item -Path $folderPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Supprimé: dossier $folder" -ForegroundColor Gray
    }
}

# Supprimer les .deps.json inutiles (garder seulement GameOverlay.App.deps.json)
$depsFiles = Get-ChildItem -Path $PublishDir -Filter "*.deps.json" -ErrorAction SilentlyContinue
foreach ($depsFile in $depsFiles) {
    if ($depsFile.Name -ne "GameOverlay.App.deps.json") {
        Remove-Item -Path $depsFile.FullName -Force -ErrorAction SilentlyContinue
        Write-Host "  Supprimé: $($depsFile.Name)" -ForegroundColor Gray
    }
}

# Calculer la taille du dossier
$size = (Get-ChildItem -Path $PublishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
$sizeMB = [math]::Round($size / 1MB, 2)

Write-Host "  OK - Nettoyage terminé" -ForegroundColor Green
Write-Host "  Taille du dossier publish: $sizeMB MB" -ForegroundColor Cyan
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "   PUBLICATION RÉUSSIE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Fichiers publiés dans: $PublishDir" -ForegroundColor Cyan
Write-Host ""




