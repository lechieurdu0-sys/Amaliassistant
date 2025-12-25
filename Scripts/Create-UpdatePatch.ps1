# Script pour créer un patch de mise à jour (ZIP avec seulement les fichiers modifiés)
# Usage: .\Create-UpdatePatch.ps1 -PreviousVersion "1.0.0.2" -NewVersion "1.0.0.3"

param(
    [Parameter(Mandatory=$true)]
    [string]$PreviousVersion,
    
    [Parameter(Mandatory=$true)]
    [string]$NewVersion,
    
    [Parameter(Mandatory=$false)]
    [string]$PreviousPublishDir = "publish_old",
    
    [Parameter(Mandatory=$false)]
    [string]$NewPublishDir = "publish"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   CREATION DU PATCH DE MISE A JOUR" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Vérifier que les dossiers existent
$newPublishPath = Join-Path $rootPath $NewPublishDir
$previousPublishPath = Join-Path $rootPath $PreviousPublishDir

if (-not (Test-Path $newPublishPath)) {
    Write-Host "ERREUR: Dossier publish introuvable: $newPublishPath" -ForegroundColor Red
    exit 1
}

Write-Host "[1/4] Analyse des fichiers..." -ForegroundColor Yellow

# Obtenir tous les fichiers de la nouvelle version
$newFiles = Get-ChildItem -Path $newPublishPath -Recurse -File | Where-Object {
    $_.Name -notlike "*.pdb" -and
    $_.Name -notlike "*.deps.json" -and
    $_.FullName -notlike "*\logs\*"
}

Write-Host "  Fichiers dans la nouvelle version: $($newFiles.Count)" -ForegroundColor Cyan

# Comparer avec l'ancienne version si elle existe
$modifiedFiles = @()
$newFilesList = @()

if (Test-Path $previousPublishPath) {
    Write-Host "  Comparaison avec la version précédente..." -ForegroundColor Cyan
    
    foreach ($newFile in $newFiles) {
        $relativePath = $newFile.FullName.Substring($newPublishPath.Length + 1)
        $oldFilePath = Join-Path $previousPublishPath $relativePath
        
        $isModified = $true
        if (Test-Path $oldFilePath) {
            $oldFile = Get-Item $oldFilePath
            $newHash = (Get-FileHash -Path $newFile.FullName -Algorithm MD5).Hash
            $oldHash = (Get-FileHash -Path $oldFilePath -Algorithm MD5).Hash
            
            if ($newHash -eq $oldHash) {
                $isModified = $false
            }
        }
        
        if ($isModified) {
            $modifiedFiles += $newFile
            $newFilesList += $relativePath
        }
    }
} else {
    Write-Host "  Aucune version précédente trouvée, tous les fichiers seront inclus" -ForegroundColor Yellow
    $modifiedFiles = $newFiles
    foreach ($file in $newFiles) {
        $relativePath = $file.FullName.Substring($newPublishPath.Length + 1)
        $newFilesList += $relativePath
    }
}

Write-Host "  Fichiers modifiés/ajoutés: $($modifiedFiles.Count)" -ForegroundColor Green

if ($modifiedFiles.Count -eq 0) {
    Write-Host "  Aucun fichier modifié, pas de patch nécessaire" -ForegroundColor Yellow
    exit 0
}

# Créer le dossier de sortie
$patchDir = Join-Path $rootPath "Patches"
if (-not (Test-Path $patchDir)) {
    New-Item -ItemType Directory -Path $patchDir | Out-Null
}

$patchFileName = "Amaliassistant_Patch_${PreviousVersion}_to_${NewVersion}.zip"
$patchPath = Join-Path $patchDir $patchFileName

Write-Host "[2/4] Création de l'archive ZIP..." -ForegroundColor Yellow

# Supprimer l'ancien patch s'il existe
if (Test-Path $patchPath) {
    Remove-Item $patchPath -Force
}

# Créer le ZIP avec seulement les fichiers modifiés
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zip = [System.IO.Compression.ZipFile]::Open($patchPath, 1)  # 1 = Create mode

try {
    foreach ($file in $modifiedFiles) {
        $relativePath = $file.FullName.Substring($newPublishPath.Length + 1)
        $entry = $zip.CreateEntry($relativePath)
        
        $entryStream = $entry.Open()
        $fileStream = [System.IO.File]::OpenRead($file.FullName)
        $fileStream.CopyTo($entryStream)
        $fileStream.Close()
        $entryStream.Close()
        
        Write-Host "  + $relativePath" -ForegroundColor Gray
    }
}
finally {
    $zip.Dispose()
}

$patchSize = (Get-Item $patchPath).Length
$patchSizeMB = [math]::Round($patchSize / 1MB, 2)

Write-Host "[3/4] Calcul des checksums..." -ForegroundColor Yellow
$patchHash = (Get-FileHash -Path $patchPath -Algorithm SHA256).Hash
Write-Host "  SHA256: $patchHash" -ForegroundColor Cyan

Write-Host "[4/4] Résumé..." -ForegroundColor Yellow
Write-Host "  Patch créé: $patchFileName" -ForegroundColor Green
Write-Host "  Taille: $patchSizeMB MB" -ForegroundColor Green
Write-Host "  Fichiers inclus: $($modifiedFiles.Count)" -ForegroundColor Green
Write-Host "  Emplacement: $patchPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   PATCH CREE AVEC SUCCES !" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Afficher les instructions
Write-Host "Pour utiliser ce patch:" -ForegroundColor Yellow
Write-Host "1. Attachez le fichier '$patchFileName' à la release GitHub" -ForegroundColor White
Write-Host "2. Mettez à jour update.xml avec l'URL du patch" -ForegroundColor White
Write-Host ""






