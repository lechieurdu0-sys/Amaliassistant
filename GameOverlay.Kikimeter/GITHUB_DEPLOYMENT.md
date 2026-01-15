# Guide de Déploiement GitHub

## Fichiers à commiter

### Code source

#### Services (PlayerManagement)
- ✅ `Services/IPlayerDataProvider.cs` - Interface pour les providers
- ✅ `Services/JsonPlayerDataProvider.cs` - Provider avec polling JSON
- ✅ `Services/LogParserPlayerDataProvider.cs` - Fallback basé sur LogParser
- ✅ `Services/PlayerManagementService.cs` - Service principal de gestion

#### Models
- ✅ `Models/PlayerStats.cs` - Modèle enrichi (modifié avec nouvelles propriétés)
- ✅ `Models/PlayerDataJson.cs` - Modèles JSON

#### Intégration
- ✅ `KikimeterWindow.cs` - Intégration principale (modifié)
- ✅ `KikimeterWindow.Reset.cs` - Gestion du reset serveur (modifié)

### Documentation

- ✅ `README.md` - README principal du projet
- ✅ `Services/INTEGRATION_GUIDE.md` - Guide d'intégration complet
- ✅ `Services/README_PlayerManagement.md` - Documentation du service
- ✅ `Services/TESTING_GUIDE.md` - Guide de test avec scénarios

### Exemples

- ✅ `Services/player_data.example.json` - Exemple de fichier JSON

## Fichiers à NE PAS commiter

### Fichiers sensibles
- ❌ `player_data.json` (fichier réel avec données utilisateur)
- ❌ Tous les tokens GitHub
- ❌ Mots de passe ou clés API
- ❌ Chemins privés spécifiques à l'utilisateur

### Fichiers générés
- ❌ `bin/` et `obj/` (dossiers de build)
- ❌ `*.user` (fichiers de configuration utilisateur)
- ❌ `*.suo` (fichiers de solution utilisateur)

## Structure recommandée pour GitHub

```
GameOverlay.Kikimeter/
├── Services/
│   ├── IPlayerDataProvider.cs
│   ├── JsonPlayerDataProvider.cs
│   ├── LogParserPlayerDataProvider.cs
│   ├── PlayerManagementService.cs
│   ├── INTEGRATION_GUIDE.md
│   ├── README_PlayerManagement.md
│   ├── TESTING_GUIDE.md
│   └── player_data.example.json
├── Models/
│   ├── PlayerStats.cs
│   └── PlayerDataJson.cs
├── KikimeterWindow.cs
├── KikimeterWindow.Reset.cs
├── README.md
└── GITHUB_DEPLOYMENT.md (ce fichier)
```

## Commandes Git recommandées

### Initialisation (si nouveau dépôt)

```bash
git init
git add .
git commit -m "Initial commit: Système de gestion automatique des joueurs"
git branch -M main
git remote add origin https://github.com/USERNAME/REPO.git
git push -u origin main
```

### Mise à jour

```bash
# Ajouter tous les fichiers modifiés
git add GameOverlay.Kikimeter/Services/IPlayerDataProvider.cs
git add GameOverlay.Kikimeter/Services/JsonPlayerDataProvider.cs
git add GameOverlay.Kikimeter/Services/LogParserPlayerDataProvider.cs
git add GameOverlay.Kikimeter/Services/PlayerManagementService.cs
git add GameOverlay.Kikimeter/Models/PlayerDataJson.cs
git add GameOverlay.Kikimeter/KikimeterWindow.cs
git add GameOverlay.Kikimeter/KikimeterWindow.Reset.cs
git add GameOverlay.Kikimeter/README.md
git add GameOverlay.Kikimeter/Services/*.md
git add GameOverlay.Kikimeter/Services/player_data.example.json

# Commit
git commit -m "feat: Système de gestion automatique des joueurs avec polling JSON

- Ajout de JsonPlayerDataProvider avec polling 1s
- Service de nettoyage automatique post-combat
- Protection du reset serveur
- Fallback LogParser si JSON absent
- Documentation complète et guide de test"

# Push
git push origin main
```

## Vérifications avant commit

### ✅ Vérifier qu'aucune information sensible n'est présente

```bash
# Chercher les tokens
grep -r "ghp_" .
grep -r "gho_" .
grep -r "github_pat" .

# Chercher les mots de passe
grep -ri "password" . --exclude-dir=.git
grep -ri "secret" . --exclude-dir=.git

# Chercher les chemins privés
grep -r "C:\\Users\\" . --exclude-dir=.git
grep -r "D:\\Users\\" . --exclude-dir=.git
```

### ✅ Vérifier que tous les fichiers nécessaires sont présents

```bash
# Vérifier les fichiers de code
ls GameOverlay.Kikimeter/Services/IPlayerDataProvider.cs
ls GameOverlay.Kikimeter/Services/JsonPlayerDataProvider.cs
ls GameOverlay.Kikimeter/Services/PlayerManagementService.cs
ls GameOverlay.Kikimeter/Models/PlayerDataJson.cs

# Vérifier la documentation
ls GameOverlay.Kikimeter/README.md
ls GameOverlay.Kikimeter/Services/INTEGRATION_GUIDE.md
ls GameOverlay.Kikimeter/Services/TESTING_GUIDE.md
```

## Tags de version recommandés

```bash
# Créer un tag pour cette version
git tag -a v1.0.0 -m "Version initiale: Système de gestion automatique des joueurs"
git push origin v1.0.0
```

## Description du commit recommandée

```
feat: Système de gestion automatique des joueurs avec polling JSON

Fonctionnalités principales:
- Nettoyage automatique post-combat des adversaires
- Gestion de groupe (max 6 joueurs)
- Préservation du personnage principal
- Synchronisation périodique (5s)
- Protection du reset serveur
- Fallback LogParser si JSON absent

Fichiers ajoutés:
- JsonPlayerDataProvider.cs (polling JSON 1s)
- PlayerManagementService.cs (nettoyage et synchronisation)
- PlayerDataJson.cs (modèles JSON)
- Documentation complète (INTEGRATION_GUIDE, TESTING_GUIDE)
- Exemple JSON (player_data.example.json)

Modifications:
- KikimeterWindow.cs (intégration du service)
- KikimeterWindow.Reset.cs (protection reset serveur)
- PlayerStats.cs (nouvelles propriétés)
```

## Checklist finale

- [ ] Tous les fichiers de code sont présents
- [ ] Toutes les documentations sont présentes
- [ ] L'exemple JSON est présent
- [ ] Aucune information sensible dans le code
- [ ] Aucun token GitHub dans les fichiers
- [ ] Aucun mot de passe ou clé API
- [ ] Aucun chemin privé spécifique
- [ ] Le README principal est à jour
- [ ] Les guides sont complets
- [ ] Les tests sont documentés

## Support

Pour toute question sur le déploiement, consulter :
- [README.md](README.md) - Vue d'ensemble
- [Services/INTEGRATION_GUIDE.md](Services/INTEGRATION_GUIDE.md) - Guide d'intégration
- [Services/TESTING_GUIDE.md](Services/TESTING_GUIDE.md) - Guide de test
