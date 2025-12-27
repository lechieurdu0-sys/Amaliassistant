# Configuration du Token GitHub

Pour utiliser les scripts qui interagissent avec GitHub (création de releases, upload de fichiers, etc.), vous devez configurer un token GitHub.

## 🔐 Méthodes de configuration (par ordre de priorité)

### 1. Variable d'environnement (Recommandé)

Définissez la variable d'environnement `GITHUB_TOKEN` :

**Windows PowerShell :**
```powershell
$env:GITHUB_TOKEN = "ghp_votre_token_ici"
```

**Windows CMD :**
```cmd
set GITHUB_TOKEN=ghp_votre_token_ici
```

**Permanent (Windows) :**
1. Ouvrez "Variables d'environnement" dans les paramètres système
2. Ajoutez `GITHUB_TOKEN` avec votre token comme valeur

### 2. Fichier local (Alternative)

Créez un fichier `TokenGitHub.txt` à la racine du projet avec votre token :

```
ghp_votre_token_ici
```

⚠️ **Important** : Ce fichier est déjà dans `.gitignore` et ne sera jamais commité.

### 3. Saisie interactive

Si aucune des méthodes ci-dessus n'est configurée, les scripts vous demanderont de saisir le token lors de l'exécution.

## 🔑 Créer un token GitHub

1. Allez sur https://github.com/settings/tokens
2. Cliquez sur "Generate new token" > "Generate new token (classic)"
3. Donnez un nom au token (ex: "Amaliassistant Releases")
4. Sélectionnez les permissions nécessaires :
   - `repo` (accès complet aux dépôts)
5. Cliquez sur "Generate token"
6. **Copiez le token immédiatement** (il ne sera plus visible après)

## 📝 Scripts utilisant le token

Les scripts suivants utilisent `Get-GitHubToken.ps1` :
- `Create-Release.ps1`
- `Create-Release-1.9.0.0.ps1`
- `Create-Release-Simple.ps1`
- `Create-Release-StepByStep.ps1`
- `EXECUTER-RELEASE.ps1`
- `Create-Release-1.0.0.11.ps1`
- `Release-Complete.ps1` (si -GitHubToken n'est pas fourni)
- `Update-ReleaseNotes.ps1`

## ⚠️ Sécurité

- **Ne jamais** commiter de tokens dans le code
- **Ne jamais** partager votre token
- Si un token est compromis, révoquez-le immédiatement sur GitHub
- Utilisez des tokens avec des permissions minimales nécessaires










