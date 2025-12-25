# Script pour obtenir la prochaine version automatiquement
# Incrémente la version patch (1.0.0.0 -> 1.0.0.1)

param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = "GameOverlay.App\GameOverlay.App.csproj",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Major", "Minor", "Build", "Revision")]
    [string]$IncrementType = "Revision"
)

$ErrorActionPreference = "Stop"

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Lire la version actuelle depuis le .csproj
$csprojPath = Join-Path $rootPath $ProjectPath
if (-not (Test-Path $csprojPath))
{
    Write-Host "ERREUR: Le fichier .csproj n'existe pas à: $csprojPath" -ForegroundColor Red
    exit 1
}

$csprojContent = Get-Content $csprojPath -Raw

# Extraire la version actuelle
$versionPattern = '<AssemblyVersion>(\d+\.\d+\.\d+\.\d+)</AssemblyVersion>'
if ($csprojContent -match $versionPattern)
{
    $currentVersion = $matches[1]
    $versionParts = $currentVersion -split '\.'
    
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $build = [int]$versionParts[2]
    $revision = [int]$versionParts[3]
    
    # Incrémenter selon le type
    switch ($IncrementType)
    {
        "Major" {
            $major++
            $minor = 0
            $build = 0
            $revision = 0
        }
        "Minor" {
            $minor++
            $build = 0
            $revision = 0
        }
        "Build" {
            $build++
            $revision = 0
        }
        "Revision" {
            $revision++
        }
    }
    
    $nextVersion = "$major.$minor.$build.$revision"
    Write-Output $nextVersion
}
else
{
    Write-Host "ERREUR: Impossible de trouver la version dans le fichier .csproj" -ForegroundColor Red
    exit 1
}






