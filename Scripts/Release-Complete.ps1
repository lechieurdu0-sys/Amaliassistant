# Script complet pour automatiser la création d'une nouvelle version
# Inclut : mise à jour version, build, création installateur, et upload GitHub des petits fichiers
# L'installateur (164 MB) doit être uploadé manuellement

param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Major", "Minor", "Build", "Revision")]
    [string]$IncrementType = "Revision",
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubToken = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipInstaller,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipUpload
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RELEASE COMPLETE - Amaliassistant" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date
# Le script est dans Scripts/, donc la racine est le parent
$rootPath = Split-Path $PSScriptRoot -Parent

# Vérifier que nous sommes dans le bon répertoire
if (-not (Test-Path (Join-Path $rootPath "GameOverlay.App\GameOverlay.App.csproj"))) {
    Write-Host "ERREUR: Ce script doit être exécuté depuis la racine du projet." -ForegroundColor Red
    exit 1
}

# Étape 0: Déterminer la version
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host "  ETAPE 0: DETERMINATION DE LA VERSION" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host ""

try {
    # Lire la version actuelle pour information
    $csprojPath = Join-Path $rootPath "GameOverlay.App\GameOverlay.App.csproj"
    if (Test-Path $csprojPath) {
        $csprojContent = Get-Content $csprojPath -Raw
        $versionPattern = '<AssemblyVersion>(\d+\.\d+\.\d+\.\d+)</AssemblyVersion>'
        if ($csprojContent -match $versionPattern) {
            $currentVersion = $matches[1]
            Write-Host "Version actuelle: $currentVersion" -ForegroundColor Cyan
        }
    }
    
    if ([string]::IsNullOrEmpty($Version)) {
        # Incrémenter automatiquement la version
        Write-Host "Aucune version spécifiée, incrémentation automatique..." -ForegroundColor Cyan
        $Version = & "$PSScriptRoot\Get-NextVersion.ps1" -IncrementType $IncrementType
        Write-Host "Nouvelle version: $Version" -ForegroundColor Green
    } else {
        Write-Host "Version spécifiée: $Version" -ForegroundColor Cyan
    }
    
    # Vérifier que la version est valide
    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        Write-Host "ERREUR: Format de version invalide. Utilisez le format X.Y.Z.W" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
} catch {
    Write-Host "ERREUR lors de la détermination de la version: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Étape 0.5: Lire la version précédente AVANT de la mettre à jour
$previousVersion = ""

# Méthode 1: Lire depuis update.xml (AVANT la mise à jour)
$updateXmlPath = Join-Path $rootPath "update.xml"
if (Test-Path $updateXmlPath) {
    try {
        $updateXml = [xml](Get-Content $updateXmlPath)
        $previousVersion = $updateXml.item.version
        if (-not [string]::IsNullOrEmpty($previousVersion)) {
            # Vérifier que la version précédente est différente de la nouvelle version
            if ($previousVersion -eq $Version) {
                Write-Host "ATTENTION: La version dans update.xml ($previousVersion) est identique à la nouvelle version ($Version)" -ForegroundColor Yellow
                Write-Host "  Tentative de détection depuis d'autres sources..." -ForegroundColor Yellow
                $previousVersion = ""  # Réinitialiser pour essayer d'autres méthodes
            } else {
                Write-Host "Version précédente détectée depuis update.xml: $previousVersion" -ForegroundColor Cyan
            }
        }
    } catch {
        # Ignorer les erreurs
    }
}

# Méthode 2: Lire depuis publish_old (si disponible)
if ([string]::IsNullOrEmpty($previousVersion)) {
    $publishOldDir = Join-Path $rootPath "publish_old"
    $dllPath = Join-Path $publishOldDir "GameOverlay.App.dll"
    if (Test-Path $dllPath) {
        try {
            $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath)
            $previousVersion = $versionInfo.FileVersion
            if (-not [string]::IsNullOrEmpty($previousVersion)) {
                Write-Host "Version précédente détectée depuis publish_old: $previousVersion" -ForegroundColor Cyan
            }
        } catch {
            # Ignorer les erreurs
        }
    }
}

# Méthode 3: Chercher le dernier patch créé (extraire la version cible)
if ([string]::IsNullOrEmpty($previousVersion)) {
    $patchesDir = Join-Path $rootPath "Patches"
    if (Test-Path $patchesDir) {
        $lastPatch = Get-ChildItem -Path $patchesDir -Filter "Amaliassistant_Patch_*_to_*.zip" | 
                     Sort-Object LastWriteTime -Descending | 
                     Select-Object -First 1
        
        if ($lastPatch) {
            # Extraire la version cible du nom du patch (c'est la version précédente pour le prochain patch)
            # Format: Amaliassistant_Patch_X.Y.Z.W_to_A.B.C.D.zip
            if ($lastPatch.Name -match 'Amaliassistant_Patch_(\d+\.\d+\.\d+\.\d+)_to_(\d+\.\d+\.\d+\.\d+)\.zip') {
                $previousVersion = $matches[2]  # La version cible du dernier patch devient la version précédente
                Write-Host "Version précédente détectée depuis le dernier patch: $previousVersion" -ForegroundColor Cyan
            }
        }
    }
}

# Vérification finale
if ([string]::IsNullOrEmpty($previousVersion)) {
    Write-Host "ATTENTION: Impossible de détecter la version précédente automatiquement" -ForegroundColor Yellow
    Write-Host "  Le patch ne pourra pas être créé sans version précédente" -ForegroundColor Yellow
} else {
    Write-Host "Version précédente confirmée: $previousVersion" -ForegroundColor Green
}

# Étape 1: Mise à jour de la version
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host "  ETAPE 1: MISE A JOUR DE LA VERSION" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host ""

try {
    $updateResult = & "$PSScriptRoot\Update-Version.ps1" -Version $Version -Silent 2>&1
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
        Write-Host "Erreur détaillée: $updateResult" -ForegroundColor Red
        throw "Échec de la mise à jour de la version"
    }
    Write-Host "OK - Version mise à jour dans tous les fichiers" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "ERREUR lors de la mise à jour de la version: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Sauvegarder l'ancienne version pour le patch (si elle existe)
$publishDir = Join-Path $rootPath "publish"
$publishOldDir = Join-Path $rootPath "publish_old"

if (Test-Path $publishDir) {
    Write-Host "Sauvegarde de l'ancienne version pour création du patch..." -ForegroundColor Cyan
    if (Test-Path $publishOldDir) {
        Remove-Item $publishOldDir -Recurse -Force
    }
    Copy-Item -Path $publishDir -Destination $publishOldDir -Recurse -Force
    Write-Host ""
}

# Étape 2: Build et Publication
if (-not $SkipBuild) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  ETAPE 2: BUILD ET PUBLICATION" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    
    try {
        & "$PSScriptRoot\Build-Release.ps1" -Clean
        if ($LASTEXITCODE -ne 0) {
            throw "Échec du build"
        }
        Write-Host ""
    } catch {
        Write-Host "ERREUR lors de la publication: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Build ignoré (SkipBuild activé)" -ForegroundColor Yellow
    Write-Host ""
}

# Étape 2.5: Création du patch (si version précédente existe)
if (-not $SkipBuild -and -not [string]::IsNullOrEmpty($Version) -and -not [string]::IsNullOrEmpty($previousVersion)) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  ETAPE 2.5: CREATION DU PATCH" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    
    try {
        $publishOldDir = Join-Path $rootPath "publish_old"
        
        if (Test-Path $publishOldDir) {
            Write-Host "  Création du patch de $previousVersion vers $Version..." -ForegroundColor Cyan
            & "$PSScriptRoot\Create-UpdatePatch.ps1" -PreviousVersion $previousVersion -NewVersion $Version
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  OK - Patch créé avec succès" -ForegroundColor Green
                
                # Mettre à jour update.xml avec l'URL du patch
                $updateXmlPath = Join-Path $rootPath "update.xml"
                if (Test-Path $updateXmlPath) {
                    $patchFileName = "Amaliassistant_Patch_${previousVersion}_to_${Version}.zip"
                    $patchUrl = "https://github.com/lechieurdu0-sys/Amaliassistant/releases/download/v$Version/$patchFileName"
                    
                    $updateXml = [xml](Get-Content $updateXmlPath)
                    $updateXml.item.patch_url = $patchUrl
                    $updateXml.Save($updateXmlPath)
                    Write-Host "  OK - update.xml mis à jour avec l'URL du patch" -ForegroundColor Green
                }
            } else {
                Write-Host "  Avertissement: Impossible de créer le patch" -ForegroundColor Yellow
            }
        } else {
            Write-Host "  Aucune version précédente trouvée, pas de patch créé" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Avertissement: Erreur lors de la création du patch: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

# Étape 3: Création de l'installateur
if (-not $SkipInstaller) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  ETAPE 3: CREATION DE L'INSTALLATEUR" -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    
    try {
        & "$PSScriptRoot\Build-Installer.ps1"
        if ($LASTEXITCODE -ne 0) {
            throw "Échec de la création de l'installateur"
        }
        Write-Host ""
    } catch {
        Write-Host "ERREUR lors de la création de l'installateur: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Création de l'installateur ignorée (SkipInstaller activé)" -ForegroundColor Yellow
    Write-Host ""
}

# Étape 4: Upload sur GitHub (petits fichiers uniquement)
if (-not $SkipUpload) {
    if (-not [string]::IsNullOrEmpty($GitHubToken)) {
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
        Write-Host "  ETAPE 4: UPLOAD SUR GITHUB" -ForegroundColor Yellow
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
        Write-Host ""
        
        try {
            $releaseParams = @{
                Version = $Version
                GitHubToken = $GitHubToken
            }
            
            if (-not [string]::IsNullOrEmpty($ReleaseNotes)) {
                $releaseParams.ReleaseNotes = $ReleaseNotes
            }
            
            # Utiliser le script qui crée la release ET upload le patch + update.xml
            & "$PSScriptRoot\Create-Release.ps1" -Version $Version -GitHubToken $GitHubToken -ReleaseNotes $ReleaseNotes
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host ""
                Write-Host "OK - Release GitHub créée et petits fichiers uploadés" -ForegroundColor Green
                
                # Valider l'URL du patch après upload
                Write-Host ""
                Write-Host "Validation de l'URL du patch..." -ForegroundColor Cyan
                & "$PSScriptRoot\Validate-PatchUrl.ps1" -Version $Version -GitHubToken $GitHubToken
            } else {
                Write-Host "AVERTISSEMENT: L'upload GitHub a échoué" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "AVERTISSEMENT: Erreur lors de l'upload GitHub: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        
        Write-Host ""
    } else {
        Write-Host "Token GitHub non fourni, upload ignoré" -ForegroundColor Yellow
        Write-Host "Utilisez -GitHubToken pour activer l'upload automatique" -ForegroundColor Yellow
        Write-Host ""
    }
} else {
    Write-Host "Upload GitHub ignoré (SkipUpload activé)" -ForegroundColor Yellow
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

Write-Host "Durée totale: $durationMinutes minutes" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Green
Write-Host ""

$publishDir = Join-Path $rootPath "publish"
$installerDir = Join-Path $rootPath "InstallerAppData"
$installerFile = Join-Path $installerDir "Amaliassistant_Setup.exe"

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

# Instructions pour l'upload manuel de l'installateur
if (Test-Path $installerFile) {
    Write-Host "PROCHAINES ETAPES:" -ForegroundColor Yellow
    Write-Host ""
    
    if (-not $SkipUpload -and -not [string]::IsNullOrEmpty($GitHubToken)) {
        Write-Host "1. Uploader l'installateur manuellement sur GitHub:" -ForegroundColor Cyan
        Write-Host "   - Fichier: $installerFile" -ForegroundColor White
        Write-Host "   - Taille: ~164 MB" -ForegroundColor White
        Write-Host "   - URL: https://github.com/lechieurdu0-sys/Amaliassistant/releases/tag/v$Version" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "2. Commiter et pousser les changements sur Git:" -ForegroundColor Cyan
        Write-Host "   git add ." -ForegroundColor White
        Write-Host "   git commit -m `"Version $Version - Release complète`"" -ForegroundColor White
        Write-Host "   git tag v$Version" -ForegroundColor White
        Write-Host "   git push origin main" -ForegroundColor White
        Write-Host "   git push origin v$Version" -ForegroundColor White
    } else {
        Write-Host "1. Créer une release sur GitHub:" -ForegroundColor Cyan
        Write-Host "   https://github.com/lechieurdu0-sys/Amaliassistant/releases/new" -ForegroundColor White
        Write-Host ""
        Write-Host "2. Tag de la release: v$Version" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "3. Attacher les fichiers suivants:" -ForegroundColor Cyan
        Write-Host "   - $installerFile (~164 MB)" -ForegroundColor White
        Write-Host "   - update.xml (depuis la racine)" -ForegroundColor White
        Write-Host ""
        Write-Host "4. Commiter et pousser les changements sur Git:" -ForegroundColor Cyan
        Write-Host "   git add ." -ForegroundColor White
        Write-Host "   git commit -m `"Version $Version - Release complète`"" -ForegroundColor White
        Write-Host "   git tag v$Version" -ForegroundColor White
        Write-Host "   git push origin main" -ForegroundColor White
        Write-Host "   git push origin v$Version" -ForegroundColor White
    }
    
    Write-Host ""
}

# Ouvrir le dossier de l'installateur
if (Test-Path $installerFile) {
    Write-Host "Souhaitez-vous ouvrir le dossier de l installateur ? (O/N)" -ForegroundColor Yellow
    $response = Read-Host
    if ($response -eq "O" -or $response -eq "o") {
        $installerDir = Split-Path -Parent $installerFile
        Invoke-Item $installerDir
    }
}

