# Script pour corriger la version si elle a été incrémentée plusieurs fois par erreur
# Utile si des builds ont échoué et ont incrémenté la version plusieurs fois

param(
    [Parameter(Mandatory=$false)]
    [string]$TargetVersion = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = "GameOverlay.App\GameOverlay.App.csproj"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   CORRECTION DE LA VERSION" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$rootPath = $PSScriptRoot
$csprojPath = Join-Path $rootPath $ProjectPath

if (-not (Test-Path $csprojPath)) {
    Write-Host "ERREUR: Le fichier .csproj n'existe pas à: $csprojPath" -ForegroundColor Red
    exit 1
}

# Lire la version actuelle
$csprojContent = Get-Content $csprojPath -Raw
$versionPattern = '<AssemblyVersion>(\d+\.\d+\.\d+\.\d+)</AssemblyVersion>'
if ($csprojContent -match $versionPattern) {
    $currentVersion = $matches[1]
    Write-Host "Version actuelle: $currentVersion" -ForegroundColor Yellow
} else {
    Write-Host "ERREUR: Impossible de trouver la version dans le fichier .csproj" -ForegroundColor Red
    exit 1
}

# Si aucune version cible n'est spécifiée, proposer la version suivante logique
if ([string]::IsNullOrEmpty($TargetVersion)) {
    $versionParts = $currentVersion -split '\.'
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $build = [int]$versionParts[2]
    $revision = [int]$versionParts[3]
    
    # Si la revision est > 9, on suppose qu'il y a eu des erreurs
    # On propose de revenir à une version logique
    if ($revision > 9) {
        Write-Host "ATTENTION: La version semble avoir été incrémentée plusieurs fois." -ForegroundColor Yellow
        Write-Host "Version actuelle: $currentVersion" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Propositions:" -ForegroundColor Cyan
        Write-Host "  1. Version logique suivante: $major.$minor.$build.10" -ForegroundColor White
        Write-Host "  2. Garder la version actuelle: $currentVersion" -ForegroundColor White
        Write-Host ""
        $choice = Read-Host "Choisissez une option (1 ou 2)"
        
        if ($choice -eq "1") {
            $TargetVersion = "$major.$minor.$build.10"
        } else {
            Write-Host "Version conservée: $currentVersion" -ForegroundColor Green
            exit 0
        }
    } else {
        Write-Host "La version semble correcte. Aucune correction nécessaire." -ForegroundColor Green
        exit 0
    }
}

# Vérifier que la version cible est valide
if ($TargetVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    Write-Host "ERREUR: Format de version invalide. Utilisez le format X.Y.Z.W" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Mise à jour vers la version: $TargetVersion" -ForegroundColor Cyan
Write-Host ""

# Mettre à jour la version
$scriptsPath = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot\Update-Version.ps1" -Version $TargetVersion

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   VERSION CORRIGEE AVEC SUCCES !" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

