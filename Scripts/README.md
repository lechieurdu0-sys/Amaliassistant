# Scripts - Amaliassistant

Ce dossier contient tous les scripts PowerShell pour la gestion du projet.

## Structure

- **Scripts principaux** : Scripts essentiels pour le développement et les releases
- **Scripts/Tests/** : Scripts de test et de vérification

## Scripts principaux

### Release-Complete.ps1
Script principal pour créer une release complète. Automatise :
- Mise à jour de version
- Build et publication
- Création de l'installateur
- Upload sur GitHub (petits fichiers)

**Usage :**
```powershell
.\Scripts\Release-Complete.ps1 -Version "1.0.0.11" -GitHubToken "VOTRE_TOKEN"
```

### Create-Release.ps1
Crée une release GitHub avec notes et upload les petits fichiers.

**Usage :**
```powershell
.\Scripts\Create-Release.ps1 -Version "1.0.0.11" -GitHubToken "VOTRE_TOKEN" -ReleaseNotes "Notes de mise à jour"
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

### Update-Version.ps1
Met à jour la version dans tous les fichiers.

**Usage :**
```powershell
.\Scripts\Update-Version.ps1 -Version "1.0.0.11"
```

### Create-UpdatePatch.ps1
Crée un patch de mise à jour (ZIP avec fichiers modifiés).

**Usage :**
```powershell
.\Scripts\Create-UpdatePatch.ps1 -PreviousVersion "1.0.0.10" -NewVersion "1.0.0.11"
```

### Autres scripts utilitaires

- **Get-NextVersion.ps1** : Obtient la prochaine version
- **Get-PreviousVersion.ps1** : Obtient la version précédente
- **Validate-PatchUrl.ps1** : Valide l'URL du patch
- **Fix-Version.ps1** : Corrige une version anormale
- **CleanProject.ps1** : Nettoie les fichiers de build

## Scripts de test

Les scripts dans `Scripts/Tests/` sont utilisés pour tester et vérifier :
- Accès GitHub
- Permissions du token
- Statut des releases
- Upload de fichiers

## Fichiers .bat

- **Release.bat** : Lance une release complète (double-clic)
- **BuildInstaller_AppData.bat** : Crée l'installateur
- **CleanPublish.bat** : Nettoie le dossier publish

## Notes importantes

⚠️ **Tous les scripts doivent être exécutés depuis la racine du projet**, pas depuis le dossier Scripts.

Les scripts utilisent `$PSScriptRoot` pour trouver leur emplacement et `Split-Path $PSScriptRoot -Parent` pour trouver la racine du projet.







