# 🎮 Amaliassistant

Application d'overlay pour **Wakfu** offrant plusieurs fonctionnalités utiles pour améliorer votre expérience de jeu.

## ✨ Fonctionnalités Principales

### 📊 Kikimeter
- Statistiques de combat en temps réel (dégâts infligés, reçus, soins, etc.)
- Affichage par joueur avec fenêtres individuelles
- Mode individuel et mode groupe
- **⚠️ Important** : Vous devez spécifier votre launcher (Steam ou Ankama Launcher) dans les paramètres

### 💰 Loot Tracker
- Suivi automatique du butin depuis les logs de chat
- Filtrage par personnage
- Statistiques détaillées

### 🌐 Navigateur Web Intégré
- Navigation web complète avec WebView2
- Mode Picture-in-Picture pour YouTube
- Recherche intelligente (Google pour les termes non-URL)
- Zoom adaptatif selon la taille de la fenêtre
- Historique de navigation sauvegardé
- Connexions sauvegardées (cookies persistants)

### 🫧 Site Bubbles
- Bulles de sites web personnalisables
- Bulles enfants pour organiser vos sites
- Déplacement en groupe
- Contrôle de l'opacité et de la taille

### ⚙️ Fenêtre Paramètres
- **Chemins de logs** : Détection automatique Steam/Ankama Launcher
- **Ordre des joueurs** : Réorganisez l'ordre d'affichage dans le Kikimeter
- **Gestion des personnages** : Liste automatique des personnages détectés dans les logs
- **Démarrage automatique** : Option pour lancer l'application au démarrage de Windows

## 🚀 Installation

1. Téléchargez le dernier installateur depuis les [Releases](../../releases)
2. Exécutez l'installateur
3. Suivez les instructions d'installation
4. Lancez l'application et configurez les chemins de logs dans les paramètres

## 📋 Prérequis

- **Windows 10/11**
- **Microsoft Edge WebView2 Runtime** (installé automatiquement si nécessaire)
- **Wakfu** avec Steam ou Ankama Launcher

## ⚙️ Configuration

### Configuration des Logs

1. Ouvrez les **Paramètres** depuis le menu principal
2. Allez dans l'onglet **"Chemins de Logs"**
3. Cliquez sur **"Steam"** ou **"Ankama Launcher"** pour la détection automatique
   - Ou utilisez **"📁 Parcourir"** pour sélectionner manuellement le fichier `wakfu.log`
4. Configurez également le chemin du log de chat (`wakfu_chat.log`) pour le Loot Tracker

### Ordre des Joueurs

1. Dans les paramètres, onglet **"Ordre des Joueurs"**
2. Sélectionnez un joueur dans la liste
3. Utilisez les boutons **▲** et **▼** pour réorganiser
4. Cliquez sur **"Valider"** pour sauvegarder

## 📖 Documentation

Pour une présentation complète de toutes les fonctionnalités, consultez :
- **[PRESENTATION_FORUM.md](PRESENTATION_FORUM.md)** : Présentation détaillée complète
- **[PRESENTATION_FORUM_COURTE.md](PRESENTATION_FORUM_COURTE.md)** : Version courte

## 🛠️ Développement

### Prérequis de Développement

- **Visual Studio 2022** ou **Visual Studio Code**
- **.NET 8.0 SDK**
- **Git** (pour cloner le dépôt)

### Compilation

```bash
# Cloner le dépôt
git clone https://github.com/VOTRE_USERNAME/Amaliassistant.git
cd Amaliassistant

# Restaurer les packages NuGet
dotnet restore

# Compiler le projet
dotnet build

# Exécuter l'application
dotnet run --project GameOverlay.App
```

### Structure du Projet

```
Amaliassistant/
├── GameOverlay.App/          # Application principale
├── GameOverlay.Kikimeter/     # Module Kikimeter
├── GameOverlay.Windows/        # Fenêtres (WebWindow, etc.)
├── GameOverlay.Models/         # Modèles de données
├── GameOverlay.Themes/         # Gestion des thèmes
├── GameOverlay.XpTracker/      # Suivi d'expérience
└── GameOverlay.ZQSD/           # Contrôles ZQSD
```

## 🐛 Signaler un Bug

Si vous rencontrez un problème, créez une [issue](../../issues) en incluant :
- Description du problème
- Étapes pour reproduire
- Version de Windows
- Logs (si disponibles)

## 💡 Suggestions

Les suggestions d'amélioration sont les bienvenues ! Créez une [issue](../../issues) avec le label "enhancement".

## 📄 Licence

[À définir - Indiquez votre licence]

## 👨‍💻 Auteur

Développé pour la communauté Wakfu.

---

**Note** : Cette application n'est pas officiellement liée à Ankama ou Wakfu. C'est un outil communautaire développé par des fans pour des fans.

