# Kikimeter - Système de Gestion Automatique des Joueurs

## Vue d'ensemble

Système robuste de gestion automatique des joueurs pour le Kikimeter Wakfu, basé sur le **polling JSON** avec fallback LogParser. Nettoie automatiquement les joueurs après chaque combat, gère les groupes (max 6 joueurs), préserve le personnage principal et maintient l'ordre de tour.

## Fonctionnalités principales

- ✅ **Nettoyage automatique post-combat** : Retire les adversaires inactifs, conserve les joueurs du groupe
- ✅ **Gestion de groupe** : Limite à 6 joueurs, préservation du personnage principal
- ✅ **Synchronisation périodique** : Mise à jour automatique toutes les 5 secondes (si aucun combat actif)
- ✅ **Protection du reset serveur** : Détection automatique et suspension du nettoyage pendant le reset
- ✅ **Fallback intelligent** : Utilise LogParser si le JSON est absent ou inaccessible
- ✅ **Gestion des cas limites** : Multi-personnages, changement de personnage principal, joueurs quittant le combat

## Architecture

```
Fichier JSON (polling 1s) → JsonPlayerDataProvider → PlayerManagementService → KikimeterWindow
                                    ↓ (fallback)
                            LogParserPlayerDataProvider
```

## Structure du projet

```
GameOverlay.Kikimeter/
├── Services/
│   ├── IPlayerDataProvider.cs              # Interface pour les providers
│   ├── JsonPlayerDataProvider.cs            # Provider avec polling JSON
│   ├── LogParserPlayerDataProvider.cs       # Fallback basé sur LogParser
│   ├── PlayerManagementService.cs           # Service principal de gestion
│   ├── INTEGRATION_GUIDE.md                # Guide d'intégration
│   ├── README_PlayerManagement.md          # Documentation du service
│   └── TESTING_GUIDE.md                     # Guide de test complet
├── Models/
│   ├── PlayerStats.cs                       # Modèle enrichi avec nouvelles propriétés
│   └── PlayerDataJson.cs                    # Modèles JSON
├── KikimeterWindow.cs                       # Intégration principale
├── KikimeterWindow.Reset.cs                 # Gestion du reset serveur
└── player_data.example.json                 # Exemple de fichier JSON
```

## Installation et configuration

### 1. Prérequis

- .NET 8.0 ou supérieur
- Application WPF compilée
- Accès en écriture à `%APPDATA%\Amaliassistant\Kikimeter\`

### 2. Configuration du fichier JSON

1. Créer le dossier : `%APPDATA%\Amaliassistant\Kikimeter\`
2. Copier `player_data.example.json` vers `player_data.json`
3. Modifier le fichier selon vos besoins

### 3. Format JSON

```json
{
  "players": [
    {
      "id": "123456",
      "name": "NomJoueur",
      "isMainCharacter": true,
      "isInGroup": true,
      "isActive": true,
      "lastSeenInCombat": "2026-01-15T10:30:00",
      "playerId": 123456
    }
  ],
  "combatActive": false,
  "lastUpdate": "2026-01-15T10:30:00",
  "serverName": "wakfu-server-1"
}
```

## Utilisation

### Intégration automatique

Le système s'intègre automatiquement dans `KikimeterWindow` :

- **Initialisation** : Dans `StartWatching()`
- **Nettoyage** : Dans `OnCombatEnded()`
- **Synchronisation** : Dans `UpdateTimer_Tick()` (toutes les 5 secondes)
- **Reset serveur** : Dans `ResetDisplayFromLoot()`

### Fallback automatique

Si le fichier JSON est absent ou inaccessible, le système utilise automatiquement `LogParserPlayerDataProvider` comme fallback, garantissant la compatibilité avec l'ancien système.

## Règles de nettoyage

### Joueurs conservés

- **Personnage principal** : Jamais retiré, même s'il est inactif
- **Joueurs du groupe** : Conservés s'ils font partie du groupe (max 6)
- **Joueurs actifs** : Conservés s'ils sont actuellement actifs dans un combat

### Joueurs retirés

- **Adversaires inactifs** : Non dans le groupe, non actifs, non vus depuis > 30s
- **Joueurs absents du JSON** : Non trouvés dans les données JSON et non dans le groupe

### Limite de groupe

- Maximum 6 joueurs dans le groupe
- Si plus de 6, les 6 plus récents sont conservés
- Le personnage principal est toujours conservé même s'il dépasse la limite

## Protection du reset serveur

Le système détecte automatiquement les changements de serveur via `serverName` dans le JSON :

1. **Détection** : Changement de `serverName` détecté automatiquement
2. **Suspension** : `BeginReset()` suspend le nettoyage et la synchronisation
3. **Réinitialisation** : `ResetDisplayFromLoot()` vide la collection
4. **Réactivation** : `EndReset()` réactive le nettoyage et la synchronisation

## Documentation

- **[INTEGRATION_GUIDE.md](Services/INTEGRATION_GUIDE.md)** : Guide d'intégration complet
- **[README_PlayerManagement.md](Services/README_PlayerManagement.md)** : Documentation détaillée du service
- **[TESTING_GUIDE.md](Services/TESTING_GUIDE.md)** : Guide de test avec scénarios

## Tests

Voir [TESTING_GUIDE.md](Services/TESTING_GUIDE.md) pour les tests complets.

### Tests rapides

1. **Nettoyage post-combat** : Vérifier que les adversaires sont retirés après un combat
2. **Limite de groupe** : Vérifier que seulement 6 joueurs sont conservés
3. **Personnage principal** : Vérifier qu'il n'est jamais retiré
4. **Reset serveur** : Vérifier que le reset fonctionne correctement
5. **Fallback** : Vérifier que LogParser est utilisé si JSON absent

## Logs

Le système génère des logs détaillés pour le débogage :

- `JsonPlayerDataProvider` : Polling et lecture JSON
- `PlayerManagementService` : Nettoyage et synchronisation
- `KikimeterWindow` : Intégration et événements

## Cas limites gérés

- ✅ Groupe complet (6 joueurs max)
- ✅ Multi-personnages
- ✅ Changement de personnage principal
- ✅ Joueurs quittant le combat
- ✅ Fichier JSON absent ou invalide
- ✅ Changement de serveur
- ✅ Fichier JSON verrouillé (en cours d'écriture)

## Contribution

1. Fork le projet
2. Créer une branche pour votre fonctionnalité (`git checkout -b feature/AmazingFeature`)
3. Commit vos changements (`git commit -m 'Add some AmazingFeature'`)
4. Push vers la branche (`git push origin feature/AmazingFeature`)
5. Ouvrir une Pull Request

## Licence

Ce projet fait partie de l'application Amaliassistant pour Wakfu.

## Support

Pour toute question ou problème, consulter la documentation dans le dossier `Services/` ou ouvrir une issue sur GitHub.

---

**Note importante** : Ce système utilise le polling JSON comme source de vérité principale, avec un fallback automatique vers LogParser si le JSON est absent. Aucune information sensible (tokens, mots de passe, chemins privés) n'est stockée dans le dépôt.
