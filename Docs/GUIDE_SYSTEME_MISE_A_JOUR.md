# Guide du Système de Mise à Jour Automatique

## 🎯 Vue d'ensemble

Le système de mise à jour est **entièrement automatisé** et fonctionne en 3 modes :

1. **Mode Patch (recommandé)** : Télécharge uniquement les fichiers modifiés (plus rapide, moins de bande passante)
2. **Mode Installateur complet (fallback)** : Si le patch échoue, télécharge l'installateur complet automatiquement
3. **Mode manuel** : L'utilisateur peut télécharger manuellement depuis GitHub

## 🔄 Processus Automatique

### Pour toi (développeur) :

1. **Faire tes modifications** dans le code
2. **Lancer la release** :
   ```powershell
   .\Release-Full.ps1 -ReleaseNotes "Description de tes modifications"
   ```
3. **Le script fait automatiquement** :
   - ✅ Incrémente la version (1.0.0.6 → 1.0.0.7)
   - ✅ Met à jour le `.csproj` avec la nouvelle version
   - ✅ Crée le patch (compare les fichiers et crée un ZIP avec seulement les fichiers modifiés)
   - ✅ Met à jour `update.xml` avec l'URL du patch
   - ✅ Compile l'application
   - ✅ Crée l'installateur
   - ✅ Crée la release GitHub
   - ✅ Upload le patch et `update.xml` sur GitHub

4. **Toi, tu dois juste** :
   - Uploader l'installateur manuellement sur GitHub (164 MB, trop gros pour upload automatique)

### Pour les utilisateurs :

1. **Au démarrage** : L'application vérifie automatiquement les mises à jour (après 3 secondes)
2. **Si une mise à jour est disponible** :
   - L'application propose de télécharger le patch (petit fichier)
   - Si le patch échoue → bascule automatiquement sur l'installateur complet
   - Si tout échoue → propose le téléchargement manuel
3. **Installation** :
   - Patch : extraction directe dans le dossier d'installation + redémarrage
   - Installateur : lance l'installateur + fermeture de l'app

## 🛡️ Gestion d'Erreurs Robuste

Le système gère automatiquement :

- ✅ **Erreur 404** (patch non trouvé) → Fallback sur installateur
- ✅ **Erreur réseau** (timeout, connexion perdue) → Fallback sur installateur
- ✅ **Fichier ZIP invalide** → Fallback sur installateur
- ✅ **Fichier vide** → Fallback sur installateur
- ✅ **Erreur d'extraction** → Fallback sur installateur
- ✅ **Timeout** → Timeout augmenté à 5 minutes pour les gros fichiers

**Résultat** : L'utilisateur aura toujours une mise à jour qui fonctionne, même si le patch pose problème.

## 📋 Checklist pour une Release

Quand tu fais une release :

1. ✅ Modifier le code
2. ✅ Lancer `.\Release-Full.ps1 -ReleaseNotes "..."` (avec le token GitHub si tu veux l'upload auto)
3. ✅ Vérifier que la release GitHub a été créée
4. ✅ **Uploader manuellement l'installateur** sur GitHub
5. ✅ Tester la mise à jour avec une version antérieure

## 🔍 Vérification

Pour vérifier qu'une release est complète :

```powershell
.\Check-ReleaseStatus.ps1
```

Ou vérifier une version spécifique :

```powershell
$headers = @{'Authorization' = 'token TON_TOKEN'; 'Accept' = 'application/vnd.github.v3+json'}
$release = Invoke-RestMethod -Uri 'https://api.github.com/repos/lechieurdu0-sys/Amaliassistant/releases/tags/v1.0.0.6' -Headers $headers
$release.assets | ForEach-Object { Write-Host "$($_.name) - $([math]::Round($_.size/1MB, 2)) MB" }
```

**Fichiers requis sur chaque release :**
- ✅ `Amaliassistant_Setup.exe` (installateur complet)
- ✅ `Amaliassistant_Patch_X_to_Y.zip` (patch, si disponible)
- ✅ `update.xml` (fichier de configuration)

## 🚀 Commandes Rapides

### Release complète avec upload automatique :
```powershell
.\Release-Full.ps1 -GitHubToken "TON_TOKEN" -ReleaseNotes "Description"
```

### Release sans upload GitHub (tu uploads manuellement) :
```powershell
.\Release-Full.ps1 -SkipGitHubRelease -ReleaseNotes "Description"
```

### Créer juste la release GitHub et uploader le patch :
```powershell
.\Create-ReleaseAndUploadPatch.ps1 -Version "1.0.0.6" -GitHubToken "TON_TOKEN" -ReleaseNotes "Description"
```

## ⚙️ Configuration

Le système utilise :
- **Version** : Lue depuis `GameOverlay.App.csproj` (AssemblyVersion)
- **URL de mise à jour** : `https://github.com/lechieurdu0-sys/Amaliassistant/releases/latest/download/update.xml`
- **Dossier d'installation** : `%APPDATA%\Amaliassistant`

## 🔧 Dépannage

### Le patch n'est pas créé automatiquement ?
- Vérifie que `publish_old` existe (sauvegarde de la version précédente)
- Le patch est créé dans `Patches\`

### L'URL du patch est vide dans update.xml ?
- Le script `Update-Version.ps1` cherche automatiquement le patch
- Vérifie que le fichier patch existe dans `Patches\` avec le bon nom

### L'upload automatique échoue ?
- Vérifie que le token GitHub a les permissions `repo`
- Les gros fichiers (installateur) doivent être uploadés manuellement

## 📝 Notes Importantes

1. **Le patch est optionnel** : Si aucun patch n'est créé, l'application utilisera l'installateur complet
2. **Le fallback est automatique** : L'utilisateur n'a rien à faire, le système bascule automatiquement
3. **Multi-écrans** : Le système de correction de position ne s'applique QUE sur un seul écran
4. **Version** : La version est incrémentée automatiquement (Revision par défaut)






