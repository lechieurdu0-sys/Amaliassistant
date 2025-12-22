# 📦 Guide de Mise à Jour Automatique

Ce guide explique comment utiliser le système de mise à jour automatique pour Amaliassistant.

## 🔄 Pour les Utilisateurs

### Vérification Automatique

L'application vérifie automatiquement les mises à jour :
- **Au démarrage** : Une vérification est effectuée 3 secondes après le lancement
- **En arrière-plan** : Aucune interruption si aucune mise à jour n'est disponible

### Vérification Manuelle

Vous pouvez vérifier manuellement les mises à jour :
1. Cliquez sur l'icône **Amaliassistant** dans la barre des tâches (zone de notification)
2. Sélectionnez **🔄 Vérifier les mises à jour** dans le menu contextuel

### Installation d'une Mise à Jour

Lorsqu'une mise à jour est disponible :
1. Un dialogue s'affiche avec les informations de la nouvelle version
2. Vous pouvez choisir :
   - **Oui** : Télécharger et installer immédiatement
   - **Non** : Ignorer pour le moment
   - **Me rappeler plus tard** : Rappel dans 1 jour

L'application se fermera automatiquement pour installer la mise à jour, puis se relancera.

## 🛠️ Pour les Développeurs

### Créer une Nouvelle Release

#### Étape 1 : Mettre à jour la Version

Exécutez le script PowerShell pour mettre à jour la version :

```powershell
.\Update-Version.ps1 -Version "1.0.1.0"
```

Ce script :
- Met à jour le fichier `update.xml` avec la nouvelle version
- Met à jour `AssemblyVersion` et `FileVersion` dans le `.csproj`

#### Étape 2 : Build et Publish

Créez la release complète :

```powershell
.\Release-Full.ps1
```

Cela génère :
- Le dossier `publish\` avec tous les fichiers
- L'installateur `InstallerAppData\Amaliassistant_Setup.exe`

#### Étape 3 : Créer la Release sur GitHub

1. Allez sur https://github.com/lechieurdu0-sys/Amaliassistant/releases/new
2. Créez un nouveau tag : `v1.0.1.0` (correspond à la version)
3. Titre de la release : `Version 1.0.1.0` (ou description personnalisée)
4. Description : Ajoutez les notes de version (corrections, nouvelles fonctionnalités, etc.)
5. **IMPORTANT** : Attachez les fichiers suivants :
   - `InstallerAppData\Amaliassistant_Setup.exe` → Nommé `Amaliassistant_Setup.exe`
   - `update.xml` → Nommé `update.xml`
6. Publiez la release

#### Étape 4 : Vérification

Les utilisateurs recevront automatiquement une notification de mise à jour lors du prochain démarrage de l'application.

### Structure du Fichier update.xml

Le fichier `update.xml` doit être présent dans chaque release GitHub et doit contenir :

```xml
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>1.0.1.0</version>
    <url>https://github.com/lechieurdu0-sys/Amaliassistant/releases/latest/download/Amaliassistant_Setup.exe</url>
    <changelog>https://github.com/lechieurdu0-sys/Amaliassistant/releases/latest</changelog>
    <mandatory>false</mandatory>
</item>
```

**Champs :**
- `version` : Numéro de version (format: X.X.X.X)
- `url` : URL directe du fichier d'installation
- `changelog` : URL de la page de release GitHub
- `mandatory` : `true` pour forcer la mise à jour, `false` pour laisser le choix

### Configuration du Service de Mise à Jour

Le service est configuré dans `GameOverlay.App/Services/UpdateService.cs` :

- **Vérification automatique** : 3 secondes après le démarrage
- **Rappel** : 1 jour si l'utilisateur choisit "Me rappeler plus tard"
- **Mode** : Normal (l'utilisateur peut choisir)
- **Admin** : Non requis pour l'installation

### Dépannage

#### Les utilisateurs ne reçoivent pas les mises à jour

1. Vérifiez que le fichier `update.xml` est bien attaché à la release GitHub
2. Vérifiez que l'URL dans `update.xml` pointe vers le bon fichier
3. Vérifiez que la version dans `update.xml` est supérieure à la version installée
4. Consultez les logs dans `%APPDATA%\Amaliassistant\logs\` pour plus de détails

#### Erreur lors du téléchargement

- Vérifiez que le fichier `Amaliassistant_Setup.exe` est bien accessible via l'URL
- Vérifiez les permissions du fichier sur GitHub
- Vérifiez la connexion Internet de l'utilisateur

## 📝 Notes Techniques

- Le système utilise une **solution personnalisée** basée sur HttpClient et l'API GitHub
- Les mises à jour sont téléchargées depuis GitHub Releases
- L'installateur est exécuté automatiquement après téléchargement
- L'application se ferme et se relance après l'installation
- Aucune dépendance externe supplémentaire requise

