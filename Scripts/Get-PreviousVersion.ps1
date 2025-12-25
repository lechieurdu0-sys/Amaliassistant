# Script pour obtenir la version précédente depuis publish_old ou update.xml (avant mise à jour)
param(
    [Parameter(Mandatory=$false)]
    [string]$PublishOldDir = "publish_old",
    
    [Parameter(Mandatory=$false)]
    [string]$UpdateXmlPath = "update.xml"
)

$ErrorActionPreference = "Stop"

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Méthode 1: Lire depuis le DLL dans publish_old (le plus fiable)
$publishOldPath = Join-Path $rootPath $PublishOldDir
$dllPath = Join-Path $publishOldPath "GameOverlay.App.dll"

if (Test-Path $dllPath) {
    try {
        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath)
        $version = $versionInfo.FileVersion
        if (-not [string]::IsNullOrEmpty($version)) {
            Write-Output $version
            exit 0
        }
    } catch {
        # Ignorer les erreurs
    }
}

# Méthode 2: Lire depuis update.xml (mais seulement si on a sauvegardé l'ancienne version)
# On ne peut pas utiliser cette méthode car update.xml est déjà mis à jour

# Méthode 3: Chercher le dernier patch créé
$patchesDir = Join-Path $rootPath "Patches"
if (Test-Path $patchesDir) {
    $lastPatch = Get-ChildItem -Path $patchesDir -Filter "Amaliassistant_Patch_*_to_*.zip" | 
                 Sort-Object LastWriteTime -Descending | 
                 Select-Object -First 1
    
    if ($lastPatch) {
        # Extraire la version cible du nom du patch
        # Format: Amaliassistant_Patch_X.Y.Z.W_to_A.B.C.D.zip
        if ($lastPatch.Name -match 'Amaliassistant_Patch_(\d+\.\d+\.\d+\.\d+)_to_(\d+\.\d+\.\d+\.\d+)\.zip') {
            $targetVersion = $matches[2]
            Write-Output $targetVersion
            exit 0
        }
    }
}

# Aucune version trouvée
Write-Output ""
exit 1

