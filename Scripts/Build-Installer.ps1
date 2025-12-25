# Script de création de l'installateur Inno Setup
# Génère l'installateur à partir du dossier publish

param(
    [string]$PublishDir = "publish",
    [string]$OutputDir = "InstallerAppData"
)

$ErrorActionPreference = "Stop"
# Le script est dans Scripts/, donc la racine est le parent
$RootPath = Split-Path $PSScriptRoot -Parent
$PublishPath = Join-Path $RootPath $PublishDir
$InstallerOutputPath = Join-Path $RootPath $OutputDir
$IssFile = Join-Path $RootPath "installer.iss"
$PrerequisitesDir = Join-Path $RootPath "Prerequisites"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   CRÉATION DE L'INSTALLATEUR" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Vérifier que le dossier publish existe
if (-not (Test-Path $PublishPath)) {
    Write-Host "ERREUR: Le dossier publish n'existe pas: $PublishPath" -ForegroundColor Red
    Write-Host "Veuillez d'abord exécuter Build-Release.ps1" -ForegroundColor Yellow
    exit 1
}

# Vérifier que le dossier Prerequisites existe
Write-Host "[1/4] Vérification des prérequis..." -ForegroundColor Yellow
if (-not (Test-Path $PrerequisitesDir)) {
    Write-Host "ERREUR: Le dossier Prerequisites n'existe pas: $PrerequisitesDir" -ForegroundColor Red
    exit 1
}

# Vérifier les fichiers de prérequis
$requiredFiles = @(
    "windowsdesktop-runtime-8.0.21-win-x64.exe",
    "windowsdesktop-runtime-8.0.21-win-x86.exe",
    "windowsdesktop-runtime-8.0.21-win-arm64.exe",
    "MicrosoftEdgeWebView2RuntimeInstallerx64.exe",
    "MicrosoftEdgeWebView2RuntimeInstallerx86.exe",
    "MicrosoftEdgeWebView2RuntimeInstallerARM64.exe"
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    $filePath = Join-Path $PrerequisitesDir $file
    if (-not (Test-Path $filePath)) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    Write-Host "ERREUR: Fichiers de prérequis manquants:" -ForegroundColor Red
    foreach ($file in $missingFiles) {
        Write-Host "  - $file" -ForegroundColor Red
    }
    exit 1
}

Write-Host "  OK - Tous les prérequis sont présents" -ForegroundColor Green
Write-Host ""

# Vérifier que le script Inno Setup existe
Write-Host "[2/4] Vérification de Inno Setup..." -ForegroundColor Yellow
$isccPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
)

$isccPath = $null
foreach ($path in $isccPaths) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if (-not $isccPath) {
    Write-Host "ERREUR: Inno Setup Compiler (ISCC.exe) introuvable." -ForegroundColor Red
    Write-Host "Veuillez installer Inno Setup depuis: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}

Write-Host "  Trouvé: $isccPath" -ForegroundColor Green
Write-Host ""

# Vérifier que le fichier .iss existe
if (-not (Test-Path $IssFile)) {
    Write-Host "ERREUR: Script Inno Setup introuvable: $IssFile" -ForegroundColor Red
    exit 1
}

# Créer le dossier de sortie
Write-Host "[3/4] Création du dossier de sortie..." -ForegroundColor Yellow
if (-not (Test-Path $InstallerOutputPath)) {
    New-Item -ItemType Directory -Path $InstallerOutputPath -Force | Out-Null
}
Write-Host "  OK - Dossier créé: $InstallerOutputPath" -ForegroundColor Green
Write-Host ""

# Compiler l'installateur
Write-Host "[4/4] Compilation de l'installateur..." -ForegroundColor Yellow

$compileArgs = @(
    "/O$InstallerOutputPath",
    $IssFile
)

Write-Host "  Commande: `"$isccPath`" /O`"$InstallerOutputPath`" `"$IssFile`"" -ForegroundColor Gray

try {
    Push-Location $RootPath
    & "$isccPath" /O"$InstallerOutputPath" "$IssFile"
    
    if ($LASTEXITCODE -ne 0) {
        throw "Échec de la compilation"
    }
    
    Write-Host "  OK - Compilation réussie" -ForegroundColor Green
} catch {
    Write-Host "  ERREUR: $($_.Exception.Message)" -ForegroundColor Red
    Pop-Location
    exit 1
} finally {
    Pop-Location
}

Write-Host ""

# Vérifier que l'installateur a été créé
$installerFile = Join-Path $InstallerOutputPath "Amaliassistant_Setup.exe"
if (Test-Path $installerFile) {
    $installerSize = (Get-Item $installerFile).Length
    $installerSizeMB = [math]::Round($installerSize / 1MB, 2)
    
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "   INSTALLATEUR CRÉÉ AVEC SUCCÈS" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Fichier: $installerFile" -ForegroundColor Cyan
    Write-Host "Taille: $installerSizeMB MB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "L'application sera installée dans: %APPDATA%\Amaliassistant" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "ATTENTION: L'installateur n'a pas été trouvé dans le dossier de sortie." -ForegroundColor Yellow
    exit 1
}








