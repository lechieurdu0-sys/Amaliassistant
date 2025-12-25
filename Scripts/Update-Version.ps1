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

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Vérifier que le fichier update.xml existe
$updateXmlPath = Join-Path $rootPath "update.xml"
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
    # Vérifier si un patch existe (chercher tous les patches qui se terminent par _to_$Version.zip)
    $patchDir = Join-Path $PSScriptRoot "Patches"
    
    # Créer le dossier Patches s'il n'existe pas
    if (-not (Test-Path $patchDir)) {
        New-Item -ItemType Directory -Path $patchDir -Force | Out-Null
    }
    
    # Chercher le patch (peut être créé avant ou après cette étape)
    $patchFile = Get-ChildItem -Path $patchDir -Filter "*_to_$Version.zip" -ErrorAction SilentlyContinue | 
                 Sort-Object LastWriteTime -Descending | 
                 Select-Object -First 1
    
    $patchUrl = ""
    if ($patchFile) {
        # Utiliser le tag de version spécifique au lieu de "latest" pour plus de fiabilité
        $patchUrl = "https://github.com/lechieurdu0-sys/Amaliassistant/releases/download/v$Version/$($patchFile.Name)"
        if (-not $Silent) {
            Write-Host "  Patch trouvé: $($patchFile.Name)" -ForegroundColor Cyan
        }
    } else {
        if (-not $Silent) {
            Write-Host "  Aucun patch trouvé pour la version $Version" -ForegroundColor Yellow
        }
    }
    
    $xmlContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>$Version</version>
    <url>https://github.com/lechieurdu0-sys/Amaliassistant/releases/latest/download/Amaliassistant_Setup.exe</url>
    <patch_url>$patchUrl</patch_url>
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
    Write-Host "[2/4] Mise à jour de la version dans le fichier .csproj..." -ForegroundColor Yellow
}
try
{
    $csprojPath = Join-Path $rootPath $ProjectPath
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

# Mettre à jour la version dans le script d'installation Inno Setup (installer.iss)
if (-not $Silent) {
    Write-Host "[3/4] Mise à jour de la version dans installer.iss..." -ForegroundColor Yellow
}
try
{
    $issPath = Join-Path $rootPath "installer.iss"
    if (Test-Path $issPath)
    {
        $issContent = Get-Content $issPath -Raw

        # Remplacer AppVersion, VersionInfoVersion et VersionInfoTextVersion
        $issContent = $issContent -replace 'AppVersion=.*', "AppVersion=$Version"
        $issContent = $issContent -replace 'VersionInfoVersion=.*', "VersionInfoVersion=$Version"
        $issContent = $issContent -replace 'VersionInfoTextVersion=.*', "VersionInfoTextVersion=$Version"
        
        # Si VersionInfoVersion n'existe pas, l'ajouter après AppVersion
        if ($issContent -notmatch 'VersionInfoVersion=')
        {
            $issContent = $issContent -replace "(AppVersion=$Version)", "`$1`nVersionInfoVersion=$Version`nVersionInfoTextVersion=$Version"
        }
        
        $issContent | Out-File -FilePath $issPath -Encoding UTF8 -NoNewline
        if (-not $Silent) {
            Write-Host "  OK - Versions mises à jour dans installer.iss (AppVersion, VersionInfoVersion, VersionInfoTextVersion)" -ForegroundColor Green
        }
    }
    else
    {
        if (-not $Silent) {
            Write-Host "  AVERTISSEMENT: installer.iss introuvable à: $issPath" -ForegroundColor Yellow
        }
    }
}
catch
{
    Write-Host "  ERREUR: Impossible de mettre à jour installer.iss: $_" -ForegroundColor Red
    exit 1
}

if (-not $Silent) {
    Write-Host "[4/4] Résumé..." -ForegroundColor Yellow
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

