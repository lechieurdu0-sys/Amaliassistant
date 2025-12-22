# Script maître pour la release complète
# Exécute toutes les étapes: nettoyage, build, publish et création de l'installateur
# Incrémente automatiquement la version si aucune version n'est spécifiée

param(
    [switch]$SkipClean,
    [switch]$SkipInstaller,
    [string]$Version = "",
    [ValidateSet("Major", "Minor", "Build", "Revision")]
    [string]$IncrementType = "Revision",
    [switch]$SkipVersionUpdate
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RELEASE COMPLETE - Amaliassistant" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date

# Vérifier que nous sommes dans le bon répertoire
$rootPath = $PSScriptRoot
if (-not (Test-Path (Join-Path $rootPath "GameOverlay.App\GameOverlay.App.csproj"))) {
    Write-Host "ERREUR: Ce script doit être exécuté depuis la racine du projet." -ForegroundColor Red
    exit 1
}

# Étape 0: Mise à jour automatique de la version
if (-not $SkipVersionUpdate) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  ETAPE 0: MISE A JOUR DE LA VERSION" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    
    try {
        if ([string]::IsNullOrEmpty($Version)) {
            # Incrémenter automatiquement la version
            Write-Host "Aucune version spécifiée, incrémentation automatique..." -ForegroundColor Cyan
            $Version = & "$rootPath\Get-NextVersion.ps1" -IncrementType $IncrementType
            Write-Host "Nouvelle version: $Version" -ForegroundColor Green
        } else {
            Write-Host "Version spécifiée: $Version" -ForegroundColor Cyan
        }
        
        # Mettre à jour la version dans le .csproj et update.xml
        $updateResult = & "$rootPath\Update-Version.ps1" -Version $Version -Silent 2>&1
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
            Write-Host "Erreur détaillée: $updateResult" -ForegroundColor Red
            throw "Échec de la mise à jour de la version"
        }
        
        Write-Host ""
    } catch {
        Write-Host ""
        Write-Host "ERREUR lors de la mise à jour de la version: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Étape 1: Build et Publication
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host "  ETAPE 1: BUILD ET PUBLICATION" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host ""

try {
    if ($SkipClean) {
        & "$rootPath\Build-Release.ps1" -SkipClean
    } else {
        & "$rootPath\Build-Release.ps1" -Clean
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Échec du build"
    }
} catch {
    Write-Host ""
    Write-Host "ERREUR lors de la publication: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Étape 2: Création de l'installateur
if (-not $SkipInstaller) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  ETAPE 2: CREATION DE L'INSTALLATEUR" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    
    try {
        & "$rootPath\Build-Installer.ps1"
        if ($LASTEXITCODE -ne 0) {
            throw "Échec de la création de l'installateur"
        }
    } catch {
        Write-Host ""
        Write-Host "ERREUR lors de la création de l'installateur: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
}

# Résumé
$endTime = Get-Date
$duration = $endTime - $startTime
$durationMinutes = [math]::Round($duration.TotalMinutes, 1)

Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host "  RESUME" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host ""

$publishDir = Join-Path $rootPath "publish"
$installerDir = Join-Path $rootPath "InstallerAppData"
$installerFile = Join-Path $installerDir "Amaliassistant_Setup.exe"

Write-Host "Durée totale: $durationMinutes minutes" -ForegroundColor Cyan
Write-Host ""

if (Test-Path $publishDir) {
    $publishSize = (Get-ChildItem -Path $publishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
    $publishSizeMB = [math]::Round($publishSize / 1MB, 2)
    Write-Host "OK Publication: $publishSizeMB MB dans '$publishDir'" -ForegroundColor Green
}

if (Test-Path $installerFile) {
    $installerSize = (Get-Item $installerFile).Length
    $installerSizeMB = [math]::Round($installerSize / 1MB, 2)
    Write-Host "OK Installateur: $installerSizeMB MB dans '$installerFile'" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   RELEASE TERMINEE AVEC SUCCES !" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Afficher les instructions pour GitHub
if (-not $SkipVersionUpdate -and -not [string]::IsNullOrEmpty($Version)) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  PROCHAINES ETAPES" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "1. Créer une release sur GitHub:" -ForegroundColor Cyan
    Write-Host "   https://github.com/lechieurdu0-sys/Amaliassistant/releases/new" -ForegroundColor White
    Write-Host ""
    Write-Host "2. Tag de la release: v$Version" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "3. Attacher les fichiers suivants:" -ForegroundColor Cyan
    Write-Host "   - Amaliassistant_Setup.exe (depuis InstallerAppData\)" -ForegroundColor White
    Write-Host "   - update.xml (depuis la racine du projet)" -ForegroundColor White
    Write-Host ""
}

# Ouvrir le dossier de l'installateur
if (Test-Path $installerFile) {
    Write-Host "Souhaitez-vous ouvrir le dossier de l'installateur ? (O/N)" -ForegroundColor Yellow
    $response = Read-Host
    if ($response -eq "O" -or $response -eq "o") {
        Start-Process explorer.exe -ArgumentList "/select,`"$installerFile`""
    }
}

