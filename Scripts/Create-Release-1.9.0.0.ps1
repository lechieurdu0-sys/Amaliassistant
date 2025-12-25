# Script pour créer la release GitHub 1.9.0.0
# Version majeure avec améliorations complètes

$ErrorActionPreference = "Stop"

# Récupérer le token GitHub de manière sécurisée
$GitHubToken = & "$PSScriptRoot\Get-GitHubToken.ps1" -RequireToken
if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    Write-Host "ERREUR: Token GitHub requis pour créer une release" -ForegroundColor Red
    exit 1
}

# Notes de release pour la version 1.9.0.0
$ReleaseNotes = @"
## Version 1.9.0 – Version majeure

### 🎉 Une base saine et refondue

Cette version marque une refonte complète du système de mise à jour et apporte des améliorations majeures à l'application. Les versions antérieures ont par conséquent été supprimées.

---

## 📋 Récapitulatif des fonctionnalités d'Amaliassistant

Amaliassistant est un overlay complet pour **Wakfu** qui offre de nombreuses fonctionnalités pour enrichir votre expérience de jeu.

### 📊 Kikimeter – Statistiques de combat en temps réel

- **Suivi complet** : Dégâts infligés, dégâts reçus, soins prodigués et boucliers
- **Détection intelligente des invocations** : Attribution automatique des actions aux maîtres (Zobal, Sadida, Osamodas, etc.)
- **Réorganisation automatique des joueurs** : Personnalisation de l'ordre pendant le combat ; possibilité de l'ajuster aussi depuis les paramètres en cas de problème
- **Barres de progression visuelles** : Affichage coloré et dynamique des statistiques

### 💰 Loot Tracker – Suivi automatique du butin

(Interface appelée à évoluer pour mieux s'intégrer à l'univers de Wakfu)

### 🔔 Notifications de ventes

- **Détection en temps réel** : Surveillance automatique des ventes effectuées
- **Alertes visuelles** : Notification pour chaque vente détectée (positionnable où vous le souhaitez via un clic droit sur le menu de l'application)
- **Calcul automatique** : Affichage du prix total des objets vendus
- **Notifications sonores** : Son personnalisable pour les ventes (volume réglable dans les paramètres)

### 🌐 Navigateur Web intégré

- **Navigation complète** : WebView2 pour une expérience web native
- **Mode Picture-in-Picture** : Prise en charge de YouTube en mode PiP
- **Recherche intelligente** : Distinction automatique entre URLs et recherche Google
- **Zoom adaptatif** : Ajustement en fonction de la taille de la fenêtre
- **Historique sauvegardé** : Conservation de l'historique de navigation
- **Cookies persistants** : Connexions maintenues entre les sessions

### ⚙️ Système de paramètres complet

- **Détection automatique des chemins** : Compatible avec Steam et Ankama Launcher
- **Gestion des logs** : Sélection manuelle ou automatique du fichier wakfu.log
- **Gestion des personnages** : Liste automatique des personnages détectés dans les logs
- **Ordre des joueurs** : Personnalisation de l'affichage dans le Kikimeter
- **Démarrage automatique** : Option pour lancer l'application au démarrage de Windows

### 🔄 Système de mise à jour automatique

- **Vérification automatique** : Détection des nouvelles versions au lancement
- **Vérification manuelle** : Option disponible dans le menu contextuel de la barre des tâches
- **Processus unifié** : Téléchargement et installation fluides en une seule étape
- **Interface moderne** : Fenêtre WPF pour le suivi des mises à jour
- **Installation automatique** : Redémarrage de l'application après mise à jour

### 🎨 Interface utilisateur

- **Overlay transparent** : Fenêtres discrètes qui n'entravent pas le gameplay
- **Thème cohérent** : Design harmonieux sur l'ensemble de l'application
- **Barre des tâches** : Icône dans la zone de notification avec menu contextuel
- **Multi-écrans** : Prise en charge des configurations à plusieurs écrans
- **Optimisé Windows 11** : Adapté pour Windows 11, tout en restant parfaitement fonctionnel sous Windows 10

---

### ✨ Améliorations de la version 1.9.0

- **Système de mise à jour unifié** : Téléchargement et installation en un seul processus fluide
- **Interface d'installation améliorée** : Fenêtre WPF moderne pour l'installation des mises à jour
- **Gestion du NotifyIcon** : Nettoyage automatique de l'icône à la fermeture
- **Stabilité renforcée** : Corrections de bugs et optimisations générales

### 🔧 Corrections techniques

- Nettoyage correct du NotifyIcon lors des mises à jour
- Robustesse accrue du système de mise à jour
- Optimisations des performances

### 📝 Notes importantes

Cette version constitue une base saine pour les développements futurs.

**Prérequis** :
- Windows 10 ou 11
- Microsoft Edge WebView2 Runtime (installé automatiquement si besoin)
- Wakfu via Steam ou Ankama Launcher

**Configuration** :
1. Lancez l'application
2. Ouvrez les Paramètres depuis le menu de la barre des tâches ou l'interface à l'écran
3. Configurez les chemins des logs (détection automatique disponible)
4. Profitez de toutes les fonctionnalités !
"@

# Créer la release
& "$PSScriptRoot\Create-Release.ps1" -Version "1.9.0.0" -GitHubToken $GitHubToken -ReleaseNotes $ReleaseNotes

Write-Host ""
Write-Host "Release créée avec succès !" -ForegroundColor Green
Write-Host "N'oubliez pas d'uploader l'installateur manuellement." -ForegroundColor Yellow
