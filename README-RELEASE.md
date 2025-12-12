# Guide de Release - Amaliassistant

Ce guide explique comment créer une version release, publier l'application et générer un installateur pour Amaliassistant.

## 📋 Prérequis

Avant de créer une release, assurez-vous d'avoir :

1. **.NET SDK 8.0** installé sur votre système
   - Vérifier avec : `dotnet --version`
   - Télécharger depuis : https://dotnet.microsoft.com/download/dotnet/8.0

2. **Inno Setup 6** (ou 5) installé
   - Télécharger depuis : https://jrsoftware.org/isdl.php
   - L'installateur par défaut est dans : `C:\Program Files (x86)\Inno Setup 6\`

3. **Fichiers de prérequis** dans le dossier `Prerequisites\` :
   - `windowsdesktop-runtime-8.0.21-win-x64.exe`
   - `windowsdesktop-runtime-8.0.21-win-x86.exe`
   - `windowsdesktop-runtime-8.0.21-win-arm64.exe`
   - `MicrosoftEdgeWebView2RuntimeInstallerx64.exe`
   - `MicrosoftEdgeWebView2RuntimeInstallerx86.exe`
   - `MicrosoftEdgeWebView2RuntimeInstallerARM64.exe`

## 🚀 Création d'une Release Complète

### Méthode 1 : Script Batch (Recommandé)

Double-cliquez sur `Release.bat` ou exécutez dans un terminal :

```batch
Release.bat
```

### Méthode 2 : Script PowerShell Direct

Ouvrez PowerShell dans le dossier du projet et exécutez :

```powershell
.\Release-Full.ps1
```

Cette commande va :
1. ✅ Nettoyer les anciens builds
2. ✅ Restaurer les packages NuGet
3. ✅ Publier l'application en mode Release
4. ✅ Nettoyer les fichiers inutiles
5. ✅ Générer l'installateur avec Inno Setup

## 📦 Étapes Individuelles

Si vous préférez exécuter les étapes séparément :

### 1. Build et Publication Seulement

```powershell
.\Build-Release.ps1
```

Options disponibles :
- `-Clean` : Nettoie complètement avant de build
- `-SkipClean` : Passe l'étape de nettoyage

Exemple :
```powershell
.\Build-Release.ps1 -Clean
```

### 2. Création de l'Installateur Seulement

```powershell
.\Build-Installer.ps1
```

⚠️ **Important** : Assurez-vous que le dossier `publish\` existe avant d'exécuter ce script.

## 📁 Structure des Dossiers

Après la release, vous aurez :

```
Projet/
├── publish/              # Application publiée (pour distribution)
├── InstallerAppData/     # Dossier de sortie de l'installateur
│   └── Amaliassistant_Setup.exe
└── Prerequisites/        # Fichiers de prérequis pour l'installateur
```

## 🔧 Personnalisation

### Changer la Version

Pour changer la version de l'application, modifiez dans `GameOverlay.App/GameOverlay.App.csproj` :

```xml
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
```

Et dans `installer.iss` :

```ini
AppVersion=1.0
```

### Configuration de l'Installateur

L'installateur est configuré dans `installer.iss`. Principales options :

- **Emplacement d'installation** : `%APPDATA%\Amaliassistant` (sans privilèges admin)
- **Compression** : LZMA2/Max (compression maximale)
- **Prérequis** : Installation automatique de .NET 8.0 et WebView2 si nécessaire
- **Langue** : Français

### Options de Publication

Les paramètres de publication sont dans `Build-Release.ps1`. Par défaut :
- Mode : Release
- Self-contained : `false` (requiert .NET Runtime installé)
- ReadyToRun : `true` (meilleures performances au démarrage)
- Debug symbols : désactivés

## 🐛 Dépannage

### Erreur : "PowerShell n'est pas disponible"

Installez PowerShell 7+ ou utilisez `powershell.exe` au lieu de `pwsh.exe`.

### Erreur : "Inno Setup introuvable"

Assurez-vous qu'Inno Setup est installé. Le script cherche automatiquement dans :
- `C:\Program Files (x86)\Inno Setup 6\`
- `C:\Program Files\Inno Setup 6\`
- `C:\Program Files (x86)\Inno Setup 5\`
- `C:\Program Files\Inno Setup 5\`

### Erreur : "Prérequis manquants"

Vérifiez que tous les fichiers sont présents dans le dossier `Prerequisites\`. Les noms de fichiers doivent correspondre exactement à ceux listés ci-dessus.

### Erreur : "dotnet publish a échoué"

1. Vérifiez que .NET SDK 8.0 est installé : `dotnet --version`
2. Vérifiez que tous les packages NuGet sont disponibles
3. Consultez les logs d'erreur pour plus de détails

### L'installateur ne s'ouvre pas

1. Vérifiez les droits d'administration si nécessaire (par défaut, pas besoin)
2. Vérifiez que l'antivirus ne bloque pas l'installateur
3. Testez sur une autre machine Windows

## 📝 Notes Importantes

- L'application est installée dans `%APPDATA%\Amaliassistant` (sans privilèges admin requis)
- L'installateur installe automatiquement .NET 8.0 Desktop Runtime et WebView2 si nécessaire
- Les fichiers de debug (.pdb) sont automatiquement supprimés lors du nettoyage
- L'installateur crée des raccourcis optionnels (bureau, menu démarrer, démarrage)

## 🎯 Résultat Final

Une fois le processus terminé, vous trouverez :

- **Installateur** : `InstallerAppData\Amaliassistant_Setup.exe`
- **Application publiée** : Dossier `publish\` (pour distribution manuelle si nécessaire)

L'installateur peut être distribué aux utilisateurs finaux. Il installe l'application et tous les prérequis nécessaires.




