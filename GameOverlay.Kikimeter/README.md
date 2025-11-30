# GameOverlay.Kikimeter

Bibliothèque d'analyse des logs de combat Wakfu avec détection automatique des invocations et attribution intelligente des actions.

## 🎯 Fonctionnalités Principales

### 📊 Statistiques de Combat
- **Dégâts infligés** : Barre rouge (#FF4444)
- **Dégâts reçus** : Barre orange (#FF8800)
- **Soins prodigués** : Barre verte (#44FF44)
- **Boucliers prodigués** : Barre bleu clair (#44AAFF)

### 🔍 Détection Multi-Patterns
Système de détection chirurgicale avec 3 types de patterns :
- **Pattern canonique** : Séquence complète 4 lignes (Masqueraider/Sadida)
- **Pattern alternatif** : Variante Osamodas avec "New summon"
- **Pattern partiel** : Détection par signatures techniques (ID négatif + breed)

### 🧠 Attribution Intelligente
- Détection automatique des invocations homonymes et hétéronymes
- Attribution transparente des actions vers les maîtres
- Gestion multi-invocations simultanées
- Cycle de vie complet avec nettoyage automatique

### 📈 Normalisation Invisible
- Barres à échelle dynamique sans régression visuelle
- Transitions lissées imperceptibles
- Pas de "vidage" ou de réajustement brutal

### 🎨 Interface Utilisateur
- Fenêtre overlay transparente et non-intrusive
- Badges visuels pour le nombre d'invoqués
- Mise à jour en temps réel
- Thème cyan cohérent avec l'application

## 🏗️ Architecture

```
GameOverlay.Kikimeter/
├── Models/                          # Modèles de données
│   ├── CombatEntity.cs             # Entités de combat
│   ├── CombatAction.cs             # Actions de combat
│   ├── EntityAssociation.cs        # Associations joueur-invoqué
│   ├── DetectedAssociation.cs      # Résultats de détection
│   └── KikimeterConfig.cs          # Configuration
├── Detectors/                       # Systèmes de détection
│   ├── SummonDetectionPattern.cs   # Patterns de détection
│   ├── MultiPatternDetectionEngine.cs  # Moteur multi-patterns
│   └── PatternLearningService.cs   # Apprentissage automatique
├── Services/                        # Services métier
│   ├── EntityRelationshipManager.cs # Registre des associations
│   └── LogFileWatcher.cs           # Surveillance des logs
├── Core/                           # Composants centraux
│   ├── ActionAttributionEngine.cs  # Moteur d'attribution
│   └── NormalizationEngine.cs      # Normalisation invisible
└── Views/                          # Interface utilisateur
    ├── KikimeterWindow.xaml        # Fenêtre principale
    ├── CharacterDisplayControl.xaml # Affichage personnage
    └── *.xaml.cs                   # Code-behind
```

## 🚀 Utilisation

```csharp
using GameOverlay.Kikimeter.Views;
using GameOverlay.Kikimeter.Models;

// Créer et afficher la fenêtre
var window = new KikimeterWindow();
window.StartMonitoring(@"C:\Path\To\Wakfu\logs.log");
window.Show();
```

## ⚙️ Configuration

Tous les paramètres sont ajustables via `KikimeterConfig` :
- Seuil de confiance minimal
- Fenêtre temporelle de détection
- Délai de nettoyage
- Facteur de lissage
- Activation des fonctionnalités

## 🧩 Extensibilité

Le système supporte :
- Ajout de nouveaux patterns via `MultiPatternDetectionEngine`
- Apprentissage automatique via `PatternLearningService`
- Personnalisation des barres et couleurs
- Intégration dans d'autres applications

## 📝 Notes Techniques

- Détection basée sur regex avec scoring de confiance
- Buffer temporel pour la reconnaissance de séquences
- Nettoyage automatique des entités inactives
- Thread-safe pour les opérations asynchrones
- Gestion robuste des logs incomplets

## 🎓 Support des Classes Wakfu

Patterns connus (avec possibilité d'apprentissage automatique) :
- **Masqueraider** : Esprit masqué (homonyme, breed 2382)
- **Osamodas** : Moogrr (hétéronyme, breed 4757)
- **Sadida** : La Sacrifiée (3747), La Gonflable (3749)

Le système peut apprendre automatiquement les patterns des 19 classes via l'analyse récursive des logs.

