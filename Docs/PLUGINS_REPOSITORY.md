# Système de Dépôt de Plugins

Le système de gestionnaire de plugins peut afficher les plugins disponibles depuis GitHub sans les télécharger automatiquement.

## Structure du fichier plugins.json

Pour que vos plugins apparaissent dans le gestionnaire, créez un fichier `plugins.json` à la racine de votre dépôt GitHub avec la structure suivante :

```json
[
  {
    "Id": "MonPlugin_ExamplePlugin",
    "Name": "Mon Plugin Exemple",
    "Version": "1.0.0",
    "Description": "Un plugin d'exemple qui fait quelque chose d'utile",
    "Author": "Votre Nom",
    "DownloadUrl": "https://github.com/lechieurdu0-sys/Amaliassistant/releases/download/v1.0.0/MonPlugin.dll"
  },
  {
    "Id": "AutrePlugin_PluginName",
    "Name": "Autre Plugin",
    "Version": "2.1.0",
    "Description": "Un autre plugin avec une description",
    "Author": "Votre Nom",
    "DownloadUrl": "https://github.com/lechieurdu0-sys/Amaliassistant/releases/download/v1.0.0/AutrePlugin.dll",
    "Changelog": "Corrections de bugs et nouvelles fonctionnalités"
  }
]
```

## Champs du JSON

- **Id** : Identifiant unique du plugin (généralement `{AssemblyName}_{ClassName}`)
- **Name** : Nom affiché du plugin
- **Version** : Version du plugin (ex: "1.0.0")
- **Description** : Description du plugin
- **Author** : Auteur du plugin
- **DownloadUrl** : URL directe de téléchargement du fichier DLL (depuis GitHub Releases ou autre)
- **Changelog** : (Optionnel) Notes de version du plugin

## Emplacement du fichier

Le fichier `plugins.json` doit être placé à la racine du dépôt GitHub et accessible via :

```
https://raw.githubusercontent.com/lechieurdu0-sys/Amaliassistant/main/plugins.json
```

Si vous utilisez une autre branche, remplacez `main` par le nom de votre branche.

## Hosting des DLL

Pour les DLL de plugins, vous avez plusieurs options :

1. **GitHub Releases** (Recommandé) :
   - Créez une release sur GitHub
   - Uploadez vos fichiers DLL
   - Utilisez l'URL de téléchargement directe dans `DownloadUrl`
   - Format : `https://github.com/{owner}/{repo}/releases/download/{tag}/{filename}.dll`

2. **Raw GitHub** :
   - Placez les DLL dans un dossier (ex: `Plugins/`)
   - Utilisez l'URL raw : `https://raw.githubusercontent.com/{owner}/{repo}/{branch}/Plugins/{filename}.dll`

3. **Autre hébergement** :
   - Tout service qui permet le téléchargement direct (GitLab, Dropbox, etc.)

## Fonctionnalités

- **Affichage** : Les plugins disponibles sont affichés dans l'onglet "Plugins disponibles"
- **Statut** : Le gestionnaire indique si un plugin est déjà installé ou si une mise à jour est disponible
- **Installation** : Les utilisateurs peuvent installer les plugins en un clic depuis l'interface
- **Pas de téléchargement automatique** : Les plugins ne sont téléchargés que si l'utilisateur clique sur "Installer"

## Exemple complet

```json
[
  {
    "Id": "KikimeterHelper_KikimeterHelper",
    "Name": "Aide Kikimeter",
    "Version": "1.2.0",
    "Description": "Ajoute des fonctionnalités supplémentaires au Kikimeter",
    "Author": "Amaliassistant Team",
    "DownloadUrl": "https://github.com/lechieurdu0-sys/Amaliassistant/releases/download/plugins-v1.0.0/KikimeterHelper.dll",
    "Changelog": "- Ajout de nouvelles statistiques\n- Correction de bugs mineurs"
  }
]
```





