# Build-WithMap.ps1
# Build complet de Amaliassistant avec le plugin integre de carte interactive Wakfu

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$pluginRepoRoot = Join-Path -Path (Split-Path $repoRoot -Parent) -ChildPath "Projet carte interactive Wakfu\PluginCarteInteractive"

Write-Host "=== Build Amaliassistant + Carte Interactive Wakfu ===" -ForegroundColor Cyan
Write-Host ""

# 1. Build du plugin carte interactive
Write-Host "[1/3] Build du plugin carte interactive..." -ForegroundColor Yellow
if (Test-Path $pluginRepoRoot) {
    Push-Location $pluginRepoRoot
    try {
        & dotnet build "WakfuInteractiveMap.sln" -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build du plugin carte echouee"
        }
        Write-Host "Plugin carte interactive compile avec succes" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Warning "Dossier du plugin introuvable: $pluginRepoRoot"
    Write-Host "Le build continuera sans le plugin carte." -ForegroundColor Yellow
}

Write-Host ""

# 2. Build de Amaliassistant (GameOverlay.sln)
Write-Host "[2/3] Build de Amaliassistant..." -ForegroundColor Yellow
& dotnet build "GameOverlay.sln" -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build de Amaliassistant echouee"
}
Write-Host "Amaliassistant compile avec succes" -ForegroundColor Green

Write-Host ""

# 3. Validate du plugin carte (si disponible)
Write-Host "[3/3] Validation du plugin carte..." -ForegroundColor Yellow
if (Test-Path $pluginRepoRoot) {
    Push-Location $pluginRepoRoot
    try {
        $cliPath = "src\Cli\WakfuInteractiveMap.Cli"
        if (Test-Path $cliPath) {
            & dotnet run --project $cliPath --no-build -- validate . --quiet
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Validation du plugin carte reussie" -ForegroundColor Green
            }
            else {
                Write-Warning "La validation du plugin carte a signale des avertissements/erreurs"
            }
        }
        else {
            Write-Host "CLI de validation introuvable, skip." -ForegroundColor Gray
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "=== Build terminee avec succes ===" -ForegroundColor Cyan
$exePath = Join-Path $repoRoot "GameOverlay.App\bin\$Configuration\net8.0-windows\GameOverlay.App.exe"
Write-Host "Executable: $exePath" -ForegroundColor White
