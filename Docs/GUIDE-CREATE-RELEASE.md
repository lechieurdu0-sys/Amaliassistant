# Guide d'utilisation de Create-Release.ps1

## Description

Ce script permet de créer automatiquement une release GitHub avec :
- ✅ Création de la release avec tag
- ✅ Ajout des notes de mise à jour (changelog)
- ✅ Upload automatique du patch (petit fichier)
- ✅ Upload automatique de `update.xml`
- ⚠️ L'installateur doit être uploadé manuellement (trop volumineux)

## Utilisation de base

```powershell
.\Create-Release.ps1 -Version "1.0.0.10" -GitHubToken "VOTRE_TOKEN"
```

## Utilisation avec notes de mise à jour

### Exemple 1 : Notes simples (une ligne)

```powershell
.\Create-Release.ps1 -Version "1.0.0.10" -GitHubToken "ghp_xxxxx" -ReleaseNotes "Corrections de bugs et améliorations de performance"
```

### Exemple 2 : Notes détaillées (multi-lignes)

```powershell
$notes = @"
## Version 1.0.0.10

### Corrections
- Correction du système de notifications de ventes en temps réel
- Amélioration de la détection des ventes (FileSystemWatcher optimisé)
- Correction de la persistance des paramètres lors des mises à jour

### Améliorations
- Nouveau système de logging catégorisé (AdvancedLogger)
- Rotation automatique des logs (3 fichiers max, 1 MB chacun)
- Archivage automatique des logs en ZIP
- Gestion automatique de l'espace disque (suppression des archives > 1 GB)

### Technique
- Amélioration de la robustesse du SaleTracker
- Retry mechanism avec exponential backoff
- Buffer FileSystemWatcher augmenté à 64 KB
"@

.\Create-Release.ps1 -Version "1.0.0.10" -GitHubToken "ghp_xxxxx" -ReleaseNotes $notes
```

### Exemple 3 : Notes depuis un fichier

```powershell
$notes = Get-Content "CHANGELOG.md" -Raw
.\Create-Release.ps1 -Version "1.0.0.10" -GitHubToken "ghp_xxxxx" -ReleaseNotes $notes
```

## Paramètres

| Paramètre | Obligatoire | Description |
|-----------|-------------|-------------|
| `-Version` | ✅ Oui | Version à créer (ex: "1.0.0.10") |
| `-GitHubToken` | ✅ Oui | Token GitHub avec permissions `repo` |
| `-ReleaseNotes` | ❌ Non | Notes de mise à jour (Markdown supporté) |
| `-Repository` | ❌ Non | Repository GitHub (défaut: "lechieurdu0-sys/Amaliassistant") |

## Format des notes de mise à jour

Les notes supportent le format Markdown. Exemple de structure recommandée :

```markdown
## Version 1.0.0.10

### 🐛 Corrections
- Correction du bug X
- Amélioration de la stabilité

### ✨ Améliorations
- Nouvelle fonctionnalité Y
- Optimisation des performances

### 🔧 Technique
- Refactoring du module Z
- Mise à jour des dépendances
```

## Ce que fait le script

1. **Vérification** : Vérifie que la version dans `update.xml` correspond
2. **Création de la release** : Crée la release GitHub avec le tag `v{Version}`
3. **Gestion des conflits** : Si le tag existe déjà, propose de supprimer l'ancienne release
4. **Upload du patch** : Upload automatiquement le patch depuis `Patches\`
5. **Upload de update.xml** : Upload automatiquement `update.xml`
6. **Résumé** : Affiche un résumé avec l'URL de la release

## Après l'exécution

Le script affichera :
- ✅ L'URL de la release créée
- ✅ La liste des fichiers uploadés
- ⚠️ Les instructions pour uploader l'installateur manuellement

**Prochaine étape manuelle :**
1. Allez sur l'URL de la release affichée
2. Uploadez `InstallerAppData\Amaliassistant_Setup.exe` (~164 MB)

## Gestion des erreurs

- **Tag déjà existant** : Le script propose de supprimer l'ancienne release
- **Patch introuvable** : Avertissement, mais la release est créée
- **Erreur d'upload** : Affiche l'erreur, mais la release reste créée

## Exemple complet pour la version 1.0.0.10

```powershell
# Préparer les notes
$releaseNotes = @"
## Version 1.0.0.10

### 🐛 Corrections
- Correction du système de notifications de ventes en temps réel
- Amélioration de la détection des ventes (FileSystemWatcher optimisé)
- Correction de la persistance des paramètres lors des mises à jour

### ✨ Améliorations
- Nouveau système de logging catégorisé (AdvancedLogger)
- Rotation automatique des logs (3 fichiers max, 1 MB chacun)
- Archivage automatique des logs en ZIP
- Gestion automatique de l'espace disque (suppression des archives > 1 GB)

### 🔧 Technique
- Amélioration de la robustesse du SaleTracker
- Retry mechanism avec exponential backoff
- Buffer FileSystemWatcher augmenté à 64 KB
- Validation automatique des URLs de patch
"@

# Créer la release
.\Create-Release.ps1 -Version "1.0.0.10" -GitHubToken "VOTRE_TOKEN" -ReleaseNotes $releaseNotes
```

## Token GitHub

Pour obtenir un token GitHub :
1. Allez sur : https://github.com/settings/tokens
2. Cliquez sur "Generate new token (classic)"
3. Sélectionnez la permission `repo`
4. Copiez le token (commence par `ghp_`)

⚠️ **Important** : Ne partagez jamais votre token GitHub publiquement !

