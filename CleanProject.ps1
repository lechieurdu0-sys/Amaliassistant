# Script de nettoyage du projet Amaliassistant
# Supprime tous les fichiers et dossiers inutiles

Write-Host "Nettoyage du projet Amaliassistant..." -ForegroundColor Cyan

# Dossiers à supprimer
$foldersToDelete = @(
    "Amaliassistant",                    # Dossier de build/installation
    "Backup_ZQSD_Tutorial_2025-10-30_23-29-45",
    "publish",
    "InstallerAppData",
    "Prerequisites",
    "GameOverlay.App\bin",
    "GameOverlay.App\obj",
    "GameOverlay.Kikimeter\bin",
    "GameOverlay.Kikimeter\obj",
    "GameOverlay.Kikimeter\kaka",        # Dossier de test
    "GameOverlay.Windows\bin",
    "GameOverlay.Windows\obj",
    "GameOverlay.Models\bin",
    "GameOverlay.Models\obj",
    "GameOverlay.Themes\bin",
    "GameOverlay.Themes\obj",
    "GameOverlay.XpTracker\bin",
    "GameOverlay.XpTracker\obj"
)

# Fichiers à supprimer
$filesToDelete = @(
    "build.log",
    "debug.log",
    "ELEMENTS_NON_UTILISES.md",
    "FONCTIONNALITES_WEB.md",
    "GUIDE_REDUCTION_TAILLE_INSTALLATEUR.md",
    "INSTRUCTIONS_IMAGE_WEB.md",
    "README_INSTALLATEUR_APPDATA.md",
    "RESUME_FINAL.md",
    "MicrosoftEdgeWebView2RuntimeInstallerARM64.exe",
    "MicrosoftEdgeWebView2RuntimeInstallerx64.exe",
    "MicrosoftEdgeWebView2RuntimeInstallerx86.exe",
    "MicrosoftEdgeWebview2Setup.exe"
)

# Supprimer les dossiers
Write-Host "`nSuppression des dossiers..." -ForegroundColor Yellow
foreach ($folder in $foldersToDelete) {
    if (Test-Path $folder) {
        Write-Host "  Suppression: $folder" -ForegroundColor Gray
        Remove-Item -Path $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Supprimer les fichiers
Write-Host "`nSuppression des fichiers..." -ForegroundColor Yellow
foreach ($file in $filesToDelete) {
    if (Test-Path $file) {
        Write-Host "  Suppression: $file" -ForegroundColor Gray
        Remove-Item -Path $file -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`nNettoyage termine !" -ForegroundColor Green

