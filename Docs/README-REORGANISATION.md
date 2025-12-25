# Réorganisation du Projet - Amaliassistant 2.0

## ✅ Réorganisation terminée

Le projet a été réorganisé pour une meilleure structure et facilité de maintenance.

## 📁 Nouvelle Structure

```
Amaliassistant 2.0/
├── Scripts/              # Tous les scripts PowerShell
│   ├── Tests/           # Scripts de test et vérification
│   └── *.ps1, *.bat     # Scripts principaux
├── Docs/                 # Toute la documentation
├── Build/                # Dossier pour les builds (vide)
├── Patches/              # Patches de mise à jour
├── InstallerAppData/     # Installateurs créés
├── Prerequisites/        # Prérequis pour l'installateur
├── publish/              # Application publiée
├── publish_old/          # Ancienne version (pour patches)
└── [Projets .NET]        # GameOverlay.App, GameOverlay.Kikimeter, etc.
```

## 🗑️ Scripts supprimés (redondants)

Les scripts suivants ont été supprimés car leurs fonctionnalités sont intégrées dans d'autres scripts :

- `Release-Full.ps1` → Remplacé par `Release-Complete.ps1`
- `Create-ReleaseAndUploadPatch.ps1` → Intégré dans `Create-Release.ps1`
- `Create-GitHubRelease.ps1` → Remplacé par `Create-Release.ps1`
- `Upload-PatchAndUpdateXml.ps1` → Intégré dans `Create-Release.ps1`
- `Upload-ReleaseFiles.ps1` → Redondant
- `Upload-UpdateXmlOnly.ps1` → Redondant
- `Upload-LargeFile.ps1` → Redondant
- `COMMANDE-RELEASE-1.0.0.10.ps1` → Spécifique à une version
- `EXECUTER-RELEASE.ps1.bak` → Fichier de backup
- `WatchSales.ps1` → Script utilitaire obsolète

## 📝 Scripts principaux

### Release-Complete.ps1
Script principal pour créer une release complète.

**Usage :**
```powershell
.\Scripts\Release-Complete.ps1 -Version "1.0.0.11" -GitHubToken "VOTRE_TOKEN"
```

### Create-Release.ps1
Crée une release GitHub avec notes et upload les petits fichiers.

**Usage :**
```powershell
.\Scripts\Create-Release.ps1 -Version "1.0.0.11" -GitHubToken "VOTRE_TOKEN" -ReleaseNotes "Notes"
```

### Build-Release.ps1
Build et publie l'application.

**Usage :**
```powershell
.\Scripts\Build-Release.ps1
```

### Build-Installer.ps1
Crée l'installateur avec Inno Setup.

**Usage :**
```powershell
.\Scripts\Build-Installer.ps1
```

## 📚 Documentation

Toute la documentation a été déplacée dans `Docs/` :
- Guides de release
- Guides GitHub
- Guides de mise à jour
- README de release

## ⚠️ Important

**Tous les scripts doivent être exécutés depuis la racine du projet**, pas depuis le dossier Scripts.

Les scripts utilisent automatiquement `Split-Path $PSScriptRoot -Parent` pour trouver la racine du projet.

## 🔧 Scripts de test

Les scripts de test sont dans `Scripts/Tests/` :
- Test-GitHubAccess.ps1
- Test-GitHubReleases.ps1
- Test-UploadSmall.ps1
- Check-ReleaseStatus.ps1
- Check-ReleaseAssets.ps1
- Check-TokenPermissions.ps1

## 📊 Statistiques

- ✅ **12 scripts principaux** déplacés dans `Scripts/`
- ✅ **7 scripts de test** déplacés dans `Scripts/Tests/`
- ✅ **10 fichiers de documentation** déplacés dans `Docs/`
- ✅ **11 scripts redondants** supprimés
- ✅ **3 fichiers .bat** déplacés dans `Scripts/`
- ✅ **Fichiers temporaires** nettoyés

## 🎯 Résultat

Le projet est maintenant :
- ✅ Plus organisé
- ✅ Plus facile à naviguer
- ✅ Moins de fichiers à la racine
- ✅ Documentation centralisée
- ✅ Scripts bien structurés

