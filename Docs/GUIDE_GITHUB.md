# 📦 Guide pour Partager l'Application sur GitHub

Ce guide vous explique étape par étape comment partager votre application Amaliassistant sur GitHub.

## 📋 Prérequis

1. **Installer Git** :
   - Téléchargez Git depuis : https://git-scm.com/download/win
   - Installez-le avec les options par défaut
   - Redémarrez votre terminal/IDE après l'installation

2. **Créer un compte GitHub** :
   - Allez sur https://github.com
   - Créez un compte gratuit si vous n'en avez pas

---

## 🚀 Étapes pour Partager le Projet

### Étape 1 : Initialiser Git dans le Projet

Ouvrez un terminal (PowerShell ou CMD) dans le dossier du projet et exécutez :

```bash
cd "D:\Users\lechi\Desktop\save - Copie - Copie"
git init
```

### Étape 2 : Ajouter les Fichiers au Dépôt

```bash
# Ajouter tous les fichiers (sauf ceux dans .gitignore)
git add .

# Vérifier ce qui sera ajouté
git status
```

### Étape 3 : Faire le Premier Commit

```bash
git commit -m "Initial commit - Amaliassistant application"
```

### Étape 4 : Créer un Dépôt sur GitHub

1. Allez sur https://github.com
2. Cliquez sur le bouton **"+"** en haut à droite → **"New repository"**
3. Remplissez les informations :
   - **Repository name** : `Amaliassistant` (ou le nom que vous préférez)
   - **Description** : "Application d'overlay pour Wakfu - Kikimeter, Loot Tracker, Web Browser, etc."
   - **Visibilité** : 
     - ✅ **Public** : Tout le monde peut voir le code
     - 🔒 **Private** : Seulement vous et les personnes que vous invitez
   - ❌ **Ne cochez PAS** "Add a README file" (vous avez déjà des fichiers)
4. Cliquez sur **"Create repository"**

### Étape 5 : Connecter le Projet Local à GitHub

GitHub vous donnera des commandes à exécuter. Utilisez celles pour un dépôt existant :

```bash
# Remplacez VOTRE_USERNAME par votre nom d'utilisateur GitHub
git remote add origin https://github.com/VOTRE_USERNAME/Amaliassistant.git

# Renommer la branche principale en "main" (si nécessaire)
git branch -M main

# Pousser le code vers GitHub
git push -u origin main
```

**Note** : GitHub vous demandera peut-être de vous authentifier. Utilisez un **Personal Access Token** (voir section ci-dessous).

---

## 🔐 Authentification GitHub

GitHub n'accepte plus les mots de passe pour Git. Vous devez utiliser un **Personal Access Token** :

### Créer un Personal Access Token

1. Allez sur https://github.com/settings/tokens
2. Cliquez sur **"Generate new token"** → **"Generate new token (classic)"**
3. Donnez-lui un nom (ex: "Amaliassistant Project")
4. Sélectionnez les permissions :
   - ✅ `repo` (accès complet aux dépôts)
5. Cliquez sur **"Generate token"**
6. **⚠️ COPIEZ LE TOKEN IMMÉDIATEMENT** (vous ne pourrez plus le voir après)

### Utiliser le Token

Quand Git vous demande votre mot de passe :
- **Username** : Votre nom d'utilisateur GitHub
- **Password** : Collez votre Personal Access Token

---

## 📝 Créer un README.md

Créez un fichier `README.md` à la racine du projet pour présenter l'application :

```markdown
# 🎮 Amaliassistant

Application d'overlay pour Wakfu offrant plusieurs fonctionnalités utiles pour les joueurs.

## ✨ Fonctionnalités

- **Kikimeter** : Statistiques de combat en temps réel
- **Loot Tracker** : Suivi automatique du butin
- **Navigateur Web Intégré** : Navigation web avec mode Picture-in-Picture
- **Site Bubbles** : Bulles de sites web personnalisables
- **Et plus encore...**

## 🚀 Installation

[Instructions d'installation]

## 📖 Documentation

Consultez les fichiers de présentation pour plus de détails :
- `PRESENTATION_FORUM.md` : Présentation complète
- `PRESENTATION_FORUM_COURTE.md` : Version courte

## 🛠️ Développement

[Instructions pour les développeurs]

## 📄 Licence

[Votre licence]
```

---

## 🔄 Commandes Git Utiles

### Voir l'État du Dépôt
```bash
git status
```

### Ajouter des Fichiers Modifiés
```bash
git add .
git commit -m "Description des modifications"
git push
```

### Voir l'Historique des Commits
```bash
git log
```

### Créer une Nouvelle Branche
```bash
git checkout -b nom-de-la-branche
```

### Revenir à la Branche Principale
```bash
git checkout main
```

---

## ⚠️ Fichiers à NE PAS Partager

Le fichier `.gitignore` exclut automatiquement :
- ✅ Fichiers compilés (`.dll`, `.exe`, `.pdb`)
- ✅ Dossiers de build (`bin/`, `obj/`)
- ✅ Fichiers de log
- ✅ Installateurs et fichiers volumineux
- ✅ Données utilisateur locales

**Vérifiez avant de pousser** que vous ne partagez pas :
- ❌ Mots de passe ou clés API
- ❌ Fichiers de configuration personnels avec des données sensibles
- ❌ Fichiers volumineux (> 100 MB)

---

## 📦 Alternatives pour les Fichiers Volumineux

Si vous devez partager des fichiers volumineux (installateurs, etc.) :

1. **GitHub Releases** : Créez une release et attachez les fichiers
2. **Git LFS** : Pour les gros fichiers binaires
3. **Autres services** : Google Drive, Dropbox, etc.

---

## 🎯 Prochaines Étapes

1. ✅ Initialiser Git
2. ✅ Créer le dépôt GitHub
3. ✅ Pousser le code
4. 📝 Créer un README.md complet
5. 🏷️ Créer des releases pour les versions
6. 📋 Ajouter des issues et un wiki si nécessaire

---

## ❓ Problèmes Courants

### "git: command not found"
→ Git n'est pas installé ou pas dans le PATH. Réinstallez Git.

### "Authentication failed"
→ Utilisez un Personal Access Token au lieu d'un mot de passe.

### "Repository not found"
→ Vérifiez que le nom du dépôt et votre nom d'utilisateur sont corrects.

### "Large file detected"
→ Utilisez Git LFS ou supprimez le fichier du commit.

---

**Besoin d'aide ?** Consultez la documentation GitHub : https://docs.github.com

