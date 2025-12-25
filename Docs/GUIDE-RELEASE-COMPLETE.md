# Guide d'utilisation - Release-Complete.ps1

## Description

Le script `Release-Complete.ps1` automatise complètement le processus de création d'une nouvelle version de l'application Amaliassistant. Il effectue :

1. **Mise à jour de la version** dans tous les fichiers (`.csproj`, `update.xml`, `installer.iss`)
2. **Build et publication** de l'application
3. **Création de l'installateur** avec Inno Setup
4. **Upload automatique sur GitHub** des petits fichiers (update.xml, patch si disponible)
5. **Instructions** pour l'upload manuel de l'installateur (164 MB)

## Utilisation de base

### Version automatique (incrémentation)

```powershell
.\Release-Complete.ps1 -GitHubToken "VOTRE_TOKEN_GITHUB"
```

Cette commande :
- Incrémente automatiquement la version (ex: 1.0.0.12 → 1.0.0.13)
- Effectue tout le processus de build
- Upload les petits fichiers sur GitHub

### Version spécifique

```powershell
.\Release-Complete.ps1 -Version "1.0.0.13" -GitHubToken "VOTRE_TOKEN_GITHUB"
```

### Avec notes de release

```powershell
.\Release-Complete.ps1 -GitHubToken "VOTRE_TOKEN_GITHUB" -ReleaseNotes "Correction des notifications de ventes et amélioration du système de logging"
```

### Sans upload GitHub

```powershell
.\Release-Complete.ps1 -SkipUpload
```

### Sans build (si déjà fait)

```powershell
.\Release-Complete.ps1 -SkipBuild -GitHubToken "VOTRE_TOKEN_GITHUB"
```

## Paramètres disponibles

| Paramètre | Description | Obligatoire | Défaut |
|-----------|-------------|------------|--------|
| `-Version` | Version spécifique (format: X.Y.Z.W) | Non | Auto-incrément |
| `-IncrementType` | Type d'incrémentation (Major/Minor/Build/Revision) | Non | Revision |
| `-GitHubToken` | Token GitHub pour l'upload | Non | "" |
| `-ReleaseNotes` | Notes de release pour GitHub | Non | "Version X.Y.Z.W" |
| `-SkipBuild` | Ignorer le build | Non | False |
| `-SkipInstaller` | Ignorer la création de l'installateur | Non | False |
| `-SkipUpload` | Ignorer l'upload GitHub | Non | False |

## Exemples d'utilisation

### Release complète avec upload

```powershell
.\Release-Complete.ps1 -GitHubToken "ghp_xxxxxxxxxxxx" -ReleaseNotes "Nouvelle version avec améliorations"
```

### Release sans upload (pour tester)

```powershell
.\Release-Complete.ps1 -SkipUpload
```

### Correction de version après erreurs

Si la version a été incrémentée plusieurs fois par erreur (ex: 1.0.0.9 → 1.0.0.12), utilisez d'abord :

```powershell
.\Fix-Version.ps1
```

Puis créez la release normalement.

## Problème de version sautée

Si vous remarquez que la version a sauté (ex: 1.0.0.9 → 1.0.0.12), c'est probablement dû à des builds qui ont échoué mais qui ont quand même incrémenté la version.

**Solution :**

1. Utilisez `Fix-Version.ps1` pour corriger la version :
   ```powershell
   .\Fix-Version.ps1
   ```

2. Ou spécifiez manuellement la version correcte :
   ```powershell
   .\Release-Complete.ps1 -Version "1.0.0.10" -GitHubToken "VOTRE_TOKEN"
   ```

## Fichiers uploadés automatiquement

Le script upload automatiquement sur GitHub :
- ✅ `update.xml` (petit fichier)
- ✅ Patch de mise à jour (si disponible dans `Patches/`)

**À uploader manuellement :**
- ⚠️ `Amaliassistant_Setup.exe` (~164 MB) - trop volumineux pour l'API GitHub

## Après la release

Une fois le script terminé, vous devez :

1. **Uploader l'installateur manuellement** sur GitHub :
   - Aller sur : https://github.com/lechieurdu0-sys/Amaliassistant/releases/tag/vX.Y.Z.W
   - Glisser-déposer `InstallerAppData\Amaliassistant_Setup.exe`

2. **Commiter et pousser sur Git** :
   ```bash
   git add .
   git commit -m "Version X.Y.Z.W - Release complète"
   git tag vX.Y.Z.W
   git push origin main
   git push origin vX.Y.Z.W
   ```

## Dépannage

### Erreur : "Token GitHub non fourni"
- Ajoutez `-GitHubToken "VOTRE_TOKEN"` à la commande
- Ou utilisez `-SkipUpload` pour ignorer l'upload

### Erreur : "Échec du build"
- Vérifiez que tous les fichiers sont sauvegardés
- Vérifiez qu'il n'y a pas d'erreurs de compilation
- Réessayez avec `-SkipBuild` si le build a déjà été fait

### Erreur : "Échec de la création de l'installateur"
- Vérifiez que Inno Setup est installé
- Vérifiez que le dossier `publish` existe et contient les fichiers

## Notes importantes

- Le script utilise `Release-Full.ps1` en arrière-plan pour certaines opérations
- L'upload de l'installateur (164 MB) doit toujours être fait manuellement
- Le script affiche toujours les instructions pour les prochaines étapes
- Les fichiers sont sauvegardés dans `InstallerAppData\` et `publish\`

