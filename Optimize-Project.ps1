# Script d'optimisation supplémentaire du projet
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   OPTIMISATION SUPPLEMENTAIRE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$rootPath = $PSScriptRoot

# 1. Déplacer README-REORGANISATION.md dans Docs
Write-Host "[1/6] Deplacement de README-REORGANISATION.md..." -ForegroundColor Yellow
$reorgReadme = Join-Path $rootPath "README-REORGANISATION.md"
if (Test-Path $reorgReadme) {
    Move-Item $reorgReadme (Join-Path $rootPath "Docs\README-REORGANISATION.md") -Force
    Write-Host "  OK - Deplace dans Docs/" -ForegroundColor Green
}
Write-Host ""

# 2. Créer dossier Config et déplacer appsettings.json
Write-Host "[2/6] Organisation des fichiers de configuration..." -ForegroundColor Yellow
$configDir = Join-Path $rootPath "Config"
New-Item -ItemType Directory -Path $configDir -Force | Out-Null

$appsettings = Join-Path $rootPath "appsettings.json"
if (Test-Path $appsettings) {
    Move-Item $appsettings (Join-Path $configDir "appsettings.json") -Force
    Write-Host "  OK - appsettings.json deplace dans Config/" -ForegroundColor Green
}
Write-Host ""

# 3. Supprimer le patch invalide
Write-Host "[3/6] Nettoyage des patches invalides..." -ForegroundColor Yellow
$invalidPatch = Join-Path $rootPath "Patches\Amaliassistant_Patch_1.0.0.10_to_1.0.0.10.zip"
if (Test-Path $invalidPatch) {
    Remove-Item $invalidPatch -Force
    Write-Host "  OK - Patch invalide supprime" -ForegroundColor Green
}
Write-Host ""

# 4. Supprimer l'ancien script de test
Write-Host "[4/6] Nettoyage des scripts de test obsoles..." -ForegroundColor Yellow
$oldTest = Join-Path $rootPath "Scripts\Tests\Check-ReleaseStatus-v1.0.0.3.ps1"
if (Test-Path $oldTest) {
    Remove-Item $oldTest -Force
    Write-Host "  OK - Ancien script de test supprime" -ForegroundColor Green
}
Write-Host ""

# 5. Fusionner les guides redondants (créer un index)
Write-Host "[5/6] Creation d'un index de documentation..." -ForegroundColor Yellow
$docsIndex = @"
# 📚 Documentation - Amaliassistant

## Guides Principaux

### 🚀 Releases et Mises à Jour
- **[GUIDE-RELEASE-COMPLETE.md](GUIDE-RELEASE-COMPLETE.md)** - Guide complet pour créer une release
- **[GUIDE-CREATE-RELEASE.md](GUIDE-CREATE-RELEASE.md)** - Guide pour créer une release GitHub
- **[GUIDE-VALIDATION-PATCH.md](GUIDE-VALIDATION-PATCH.md)** - Guide de validation des patches
- **[GUIDE_SYSTEME_MISE_A_JOUR.md](GUIDE_SYSTEME_MISE_A_JOUR.md)** - Système de mise à jour automatique

### 🔧 GitHub
- **[GUIDE_GITHUB_RELEASE.md](GUIDE_GITHUB_RELEASE.md)** - Automatisation des releases GitHub
- **[GUIDE_GITHUB.md](GUIDE_GITHUB.md)** - Guide général GitHub (basique)

### 📝 Autres
- **[README-RELEASE.md](README-RELEASE.md)** - Documentation des releases
- **[README-REORGANISATION.md](README-REORGANISATION.md)** - Notes de réorganisation du projet

## Guides Textuels (Legacy)
- **GUIDE-RELEASE.txt** - Guide rapide (format texte)
- **COMMANDE-RELEASE.txt** - Commandes de release

## 📖 Note
Les guides en format Markdown (.md) sont plus à jour et détaillés que les versions texte (.txt).
"@

$docsIndexPath = Join-Path $rootPath "Docs\INDEX.md"
$docsIndex | Out-File -FilePath $docsIndexPath -Encoding UTF8
Write-Host "  OK - Index de documentation cree" -ForegroundColor Green
Write-Host ""

# 6. Créer un README principal amélioré
Write-Host "[6/6] Mise a jour du README principal..." -ForegroundColor Yellow
$mainReadme = @"
# 🎮 Amaliassistant

Application d'overlay pour Wakfu offrant plusieurs fonctionnalités utiles pour améliorer votre expérience de jeu.

## ✨ Fonctionnalités Principales

### 📊 Kikimeter
Statistiques de combat en temps réel (dégâts infligés, reçus, soins, etc.)

**⚠️ Important** : Vous devez spécifier votre launcher (Steam ou Ankama Launcher) dans les paramètres

### 💰 Loot Tracker
Suivi automatique du butin depuis les logs de chat
- Filtrage par personnage
- Statistiques détaillées
- Notification d'objets vendus hors connexion et pendant la session de jeu avec le prix total

### 🌐 Navigateur Web Intégré
Navigation web complète avec WebView2
- Mode Picture-in-Picture pour YouTube
- Recherche intelligente (Google pour les termes non-URL)
- Zoom adaptatif selon la taille de la fenêtre
- Historique de navigation sauvegardé
- Connexions sauvegardées (cookies persistants)

### ⚙️ Fenêtre Paramètres
- **Chemins de logs** : Détection automatique Steam/Ankama Launcher
- **Ordre des joueurs** : Réorganisez l'ordre d'affichage dans le Kikimeter
- **Gestion des personnages** : Liste automatique des personnages détectés dans les logs
- **Démarrage automatique** : Option pour lancer l'application au démarrage de Windows

## 🚀 Installation

1. Téléchargez le dernier installateur depuis les [Releases GitHub](https://github.com/lechieurdu0-sys/Amaliassistant/releases)
2. Exécutez l'installateur
3. Suivez les instructions d'installation
4. Lancez l'application et configurez les chemins de logs dans les paramètres

## 📋 Prérequis

- Windows 10/11
- Microsoft Edge WebView2 Runtime (installé automatiquement si nécessaire)
- Wakfu avec Steam ou Ankama Launcher

## ⚙️ Configuration

### Configuration des Logs

1. Ouvrez les Paramètres depuis le menu principal
2. Allez dans l'onglet "Chemins de Logs"
3. Cliquez sur "Steam" ou "Ankama Launcher" pour la détection automatique
4. Ou utilisez "📁 Parcourir" pour sélectionner manuellement le fichier wakfu.log

## 🛠️ Développement

### Structure du Projet

\`\`\`
Amaliassistant 2.0/
├── Scripts/          # Scripts PowerShell pour build/release
├── Docs/             # Documentation complète
├── Config/           # Fichiers de configuration
├── Patches/          # Patches de mise à jour
└── [Projets .NET]    # Code source
\`\`\`

### Scripts Principaux

- \`Scripts\Release-Complete.ps1\` - Release complète automatisée
- \`Scripts\Create-Release.ps1\` - Création release GitHub
- \`Scripts\Build-Release.ps1\` - Build de l'application
- \`Scripts\Build-Installer.ps1\` - Création installateur

Voir [Docs/INDEX.md](Docs/INDEX.md) pour la documentation complète.

## 📚 Documentation

Toute la documentation est disponible dans le dossier \`Docs/\` :
- Guides de release et mise à jour
- Guides GitHub
- Documentation technique

Consultez [Docs/INDEX.md](Docs/INDEX.md) pour un index complet.

## 📄 Licence

[Votre licence]

## 🔗 Liens

- [Releases GitHub](https://github.com/lechieurdu0-sys/Amaliassistant/releases)
- [Issues](https://github.com/lechieurdu0-sys/Amaliassistant/issues)
"@

$mainReadmePath = Join-Path $rootPath "README.md"
$mainReadme | Out-File -FilePath $mainReadmePath -Encoding UTF8
Write-Host "  OK - README principal mis a jour" -ForegroundColor Green
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   OPTIMISATION TERMINEE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Optimisations effectuees:" -ForegroundColor Yellow
Write-Host "  ✓ Documentation reorganisee" -ForegroundColor Green
Write-Host "  ✓ Fichiers de config organises" -ForegroundColor Green
Write-Host "  ✓ Patches invalides supprimes" -ForegroundColor Green
Write-Host "  ✓ Scripts obsoles supprimes" -ForegroundColor Green
Write-Host "  ✓ Index de documentation cree" -ForegroundColor Green
Write-Host "  ✓ README principal ameliore" -ForegroundColor Green
Write-Host ""















