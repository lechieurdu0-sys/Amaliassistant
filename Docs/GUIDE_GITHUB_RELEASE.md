# 📦 Guide d'Automatisation des Releases GitHub

Ce guide explique comment automatiser complètement la création de releases GitHub avec upload des fichiers.

## 🔑 Configuration du Token GitHub

### 1. Créer un Personal Access Token (PAT)

1. Allez sur https://github.com/settings/tokens
2. Cliquez sur **"Generate new token"** → **"Generate new token (classic)"**
3. Donnez un nom au token (ex: "Amaliassistant Release Automation")
4. Sélectionnez les permissions :
   - ✅ **`repo`** (accès complet aux dépôts privés)
5. Cliquez sur **"Generate token"**
6. **Copiez le token immédiatement** (il ne sera plus visible après)

### 2. Configurer le Token

**Option 1 : Variable d'environnement (Recommandé)**

```powershell
# Windows PowerShell
[System.Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "ghp_votre_token_ici", "User")

# Ou temporairement pour la session
$env:GITHUB_TOKEN = "ghp_votre_token_ici"
```

**Option 2 : Passer le token en paramètre**

```powershell
.\Release-Full.ps1 -GitHubToken "ghp_votre_token_ici"
```

## 🚀 Utilisation

### Release Complète Automatique

```powershell
# Release complète avec upload GitHub automatique
.\Release-Full.ps1 -GitHubToken "ghp_votre_token_ici"
```

Le script va :
1. ✅ Incrémenter automatiquement la version
2. ✅ Mettre à jour le `.csproj` et `update.xml`
3. ✅ Build et publish
4. ✅ Créer l'installateur
5. ✅ Créer la release GitHub
6. ✅ Uploader `Amaliassistant_Setup.exe`
7. ✅ Uploader `update.xml`

### Options Disponibles

```powershell
# Spécifier une version manuelle
.\Release-Full.ps1 -Version "1.0.1.0" -GitHubToken "ghp_xxx"

# Ajouter des notes de release
.\Release-Full.ps1 -ReleaseNotes "Correction du bug du Kikimeter" -GitHubToken "ghp_xxx"

# Ignorer l'upload GitHub (créer la release manuellement)
.\Release-Full.ps1 -SkipGitHubRelease

# Ignorer la mise à jour de version
.\Release-Full.ps1 -SkipVersionUpdate -Version "1.0.1.0" -GitHubToken "ghp_xxx"
```

### Script Standalone pour GitHub

Si vous voulez juste créer/uploader une release sans rebuild :

```powershell
.\Create-GitHubRelease.ps1 -Version "1.0.1.0" -GitHubToken "ghp_xxx" -ReleaseNotes "Notes de la release"
```

## 🔒 Sécurité

⚠️ **IMPORTANT** : Ne commitez jamais votre token GitHub dans le dépôt !

- Utilisez une variable d'environnement
- Ou passez le token en paramètre (il ne sera pas affiché dans l'historique)
- Le token doit avoir uniquement les permissions `repo`

## 📝 Exemple Complet

```powershell
# 1. Configurer le token une fois (optionnel)
$env:GITHUB_TOKEN = "ghp_votre_token_ici"

# 2. Lancer la release complète
.\Release-Full.ps1 -ReleaseNotes "Nouvelle fonctionnalité: Mise à jour automatique"

# Résultat :
# - Version incrémentée automatiquement
# - Build et installateur créés
# - Release GitHub créée avec tag v1.0.0.X
# - Fichiers uploadés automatiquement
```

## 🐛 Dépannage

### Erreur : "Token GitHub requis"
- Vérifiez que le token est configuré (variable d'environnement ou paramètre)
- Vérifiez que le token a les permissions `repo`

### Erreur : "Release existe déjà"
- Le script détecte automatiquement si la release existe et la réutilise
- Si vous voulez forcer une nouvelle release, supprimez l'ancienne sur GitHub

### Erreur lors de l'upload
- Vérifiez que les fichiers existent (`InstallerAppData\Amaliassistant_Setup.exe` et `update.xml`)
- Vérifiez que le token a les permissions d'écriture sur le dépôt






