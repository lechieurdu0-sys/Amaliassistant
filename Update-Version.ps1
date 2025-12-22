# Script pour mettre à jour le fichier update.xml avec la nouvelle version
# Usage: .\Update-Version.ps1 -Version "1.0.1.0"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = "GameOverlay.App\GameOverlay.App.csproj",
    
    [Parameter(Mandatory=$false)]
    [switch]$Silent
)

$ErrorActionPreference = "Stop"

if (-not $Silent) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   MISE A JOUR DE LA VERSION" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

# Vérifier que le fichier update.xml existe
$updateXmlPath = Join-Path $PSScriptRoot "update.xml"
if (-not (Test-Path $updateXmlPath))
{
    Write-Host "ERREUR: Le fichier update.xml n'existe pas à: $updateXmlPath" -ForegroundColor Red
    exit 1
}

# Mettre à jour le fichier update.xml
if (-not $Silent) {
    Write-Host "[1/3] Mise à jour du fichier update.xml..." -ForegroundColor Yellow
}
try
{
    $xmlContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>$Version</version>
    <url>https://github.com/lechieurdu0-sys/Amaliassistant/releases/latest/download/Amaliassistant_Setup.exe</url>
    <changelog>https://github.com/lechieurdu0-sys/Amaliassistant/releases/latest</changelog>
    <mandatory>false</mandatory>
</item>
"@
    
    $xmlContent | Out-File -FilePath $updateXmlPath -Encoding UTF8 -NoNewline
    if (-not $Silent) {
        Write-Host "  OK - Fichier update.xml mis à jour avec la version $Version" -ForegroundColor Green
    }
}
catch
{
    Write-Host "  ERREUR: Impossible de mettre à jour update.xml: $_" -ForegroundColor Red
    exit 1
}

# Mettre à jour la version dans le fichier .csproj
if (-not $Silent) {
    Write-Host "[2/3] Mise à jour de la version dans le fichier .csproj..." -ForegroundColor Yellow
}
try
{
    $csprojPath = Join-Path $PSScriptRoot $ProjectPath
    if (-not (Test-Path $csprojPath))
    {
        if (-not $Silent) {
            Write-Host "  AVERTISSEMENT: Le fichier .csproj n'existe pas à: $csprojPath" -ForegroundColor Yellow
        }
    }
    else
    {
        $csprojContent = Get-Content $csprojPath -Raw
        
        # Extraire la version actuelle (format: 1.0.0.0)
        $versionPattern = '<AssemblyVersion>(\d+\.\d+\.\d+\.\d+)</AssemblyVersion>'
        $fileVersionPattern = '<FileVersion>(\d+\.\d+\.\d+\.\d+)</FileVersion>'
        
        if ($csprojContent -match $versionPattern)
        {
            $csprojContent = $csprojContent -replace $versionPattern, "<AssemblyVersion>$Version</AssemblyVersion>"
            if (-not $Silent) {
                Write-Host "  OK - AssemblyVersion mis à jour" -ForegroundColor Green
            }
        }
        
        if ($csprojContent -match $fileVersionPattern)
        {
            $csprojContent = $csprojContent -replace $fileVersionPattern, "<FileVersion>$Version</FileVersion>"
            if (-not $Silent) {
                Write-Host "  OK - FileVersion mis à jour" -ForegroundColor Green
            }
        }
        
        $csprojContent | Out-File -FilePath $csprojPath -Encoding UTF8 -NoNewline
    }
}
catch
{
    Write-Host "  ERREUR: Impossible de mettre à jour le .csproj: $_" -ForegroundColor Red
    exit 1
}

if (-not $Silent) {
    Write-Host "[3/3] Résumé..." -ForegroundColor Yellow
    Write-Host "  Version définie: $Version" -ForegroundColor Green
    Write-Host "  Fichier update.xml: $updateXmlPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   MISE A JOUR TERMINEE" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "N'oubliez pas de:" -ForegroundColor Yellow
    Write-Host "  1. Créer une release sur GitHub avec le tag v$Version" -ForegroundColor Yellow
    Write-Host "  2. Attacher le fichier Amaliassistant_Setup.exe à la release" -ForegroundColor Yellow
    Write-Host "  3. Attacher le fichier update.xml à la release" -ForegroundColor Yellow
    Write-Host ""
}

