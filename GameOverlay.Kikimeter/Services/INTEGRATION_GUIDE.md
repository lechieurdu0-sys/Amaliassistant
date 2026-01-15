# Guide d'Intégration - Gestion Automatique des Joueurs

## Vue d'ensemble

Ce système gère automatiquement les joueurs dans le Kikimeter et l'Ordre des joueurs en utilisant le **polling JSON** comme source de vérité. Il nettoie automatiquement les joueurs après chaque combat tout en préservant le reset serveur existant.

## Architecture

### Composants

1. **`IPlayerDataProvider`** : Interface pour fournir les données des joueurs
2. **`JsonPlayerDataProvider`** : Implémentation avec polling JSON (1 seconde par défaut)
3. **`LogParserPlayerDataProvider`** : Fallback basé sur le LogParser existant
4. **`PlayerManagementService`** : Service principal de gestion et nettoyage
5. **`PlayerStats`** : Modèle enrichi avec `IsInGroup`, `IsMainCharacter`, `IsActive`, `LastSeenInCombat`

### Flux de données

```
Fichier JSON → JsonPlayerDataProvider (polling) → PlayerManagementService → KikimeterWindow
```

## Format JSON

Le fichier JSON doit être situé dans : `%APPDATA%\Amaliassistant\Kikimeter\player_data.json`

Format attendu :

```json
{
  "players": [
    {
      "id": "123456",
      "name": "NomJoueur",
      "isMainCharacter": true,
      "isInGroup": true,
      "isActive": true,
      "lastSeenInCombat": "2024-01-15T10:30:00",
      "playerId": 123456
    }
  ],
  "combatActive": false,
  "lastUpdate": "2024-01-15T10:30:00",
  "serverName": "wakfu-server-1"
}
```

## Règles de nettoyage

### Après chaque combat (`OnCombatEnded`)

1. **Joueurs conservés** :
   - Personnage principal (jamais retiré)
   - Joueurs du groupe (max 6, les plus récents)
   - Joueurs actifs dans un combat

2. **Joueurs retirés** :
   - Adversaires inactifs (non dans le groupe, non actifs, non vus depuis > 30s)
   - Joueurs absents du JSON et non dans le groupe

3. **Limite de groupe** :
   - Maximum 6 joueurs dans le groupe
   - Si plus de 6, les 6 plus récents sont conservés
   - Le personnage principal est toujours conservé même s'il dépasse la limite

### Synchronisation périodique

- Toutes les 5 secondes (si aucun combat actif)
- Met à jour les joueurs depuis le JSON
- Ajoute les nouveaux joueurs détectés
- Retire les joueurs inactifs

## Protection du reset serveur

Le système détecte automatiquement les changements de serveur et suspend le nettoyage pendant le reset :

1. **Détection** : Via `serverName` dans le JSON
2. **Suspension** : `BeginReset()` / `EndReset()` dans `ResetDisplayFromLoot()`
3. **Réactivation** : Automatique après le reset

## Intégration dans KikimeterWindow

### Initialisation

```csharp
// Dans StartWatching()
try
{
    _playerDataProvider = new JsonPlayerDataProvider();
}
catch
{
    // Fallback sur LogParserPlayerDataProvider
    _playerDataProvider = new LogParserPlayerDataProvider(_logWatcher.Parser);
}

_playerManagementService = new PlayerManagementService(_playerDataProvider);
```

### Nettoyage après combat

```csharp
// Dans OnCombatEnded()
_playerManagementService.CleanupPlayersAfterCombat(_playersCollection, DateTime.Now);
```

### Synchronisation périodique

```csharp
// Dans UpdateTimer_Tick() (toutes les 5 secondes si aucun combat actif)
_playerManagementService.SyncPlayersWithJson(_playersCollection);
```

### Reset serveur

```csharp
// Dans ResetDisplayFromLoot()
_playerManagementService.BeginReset();
// ... reset logic ...
_playerManagementService.EndReset();
```

## Cas limites gérés

### Groupe complet (6 joueurs)

- Si un nouveau joueur rejoint le groupe, le moins récent est retiré
- Le personnage principal est toujours conservé

### Multi-personnages

- Un seul personnage principal à la fois
- Changement de personnage principal géré automatiquement

### Changement de personnage principal

- L'ancien personnage principal devient un joueur normal
- Le nouveau personnage principal est préservé

### Joueurs quittant le combat

- Retirés automatiquement après 30 secondes d'inactivité
- Sauf s'ils sont dans le groupe ou sont le personnage principal

## Logs

Le système génère des logs détaillés :

- `JsonPlayerDataProvider` : Polling et lecture JSON
- `PlayerManagementService` : Nettoyage et synchronisation
- `KikimeterWindow` : Intégration et événements

## Tests

### Test 1 : Nettoyage après combat

1. Démarrer un combat avec plusieurs joueurs (groupe + adversaires)
2. Terminer le combat
3. Vérifier que les adversaires sont retirés
4. Vérifier que les joueurs du groupe sont conservés

### Test 2 : Limite de groupe

1. Ajouter 7 joueurs au groupe
2. Vérifier que seulement 6 sont conservés (les plus récents)
3. Vérifier que le personnage principal est toujours présent

### Test 3 : Reset serveur

1. Changer de serveur (modifier `serverName` dans le JSON)
2. Vérifier que le reset est déclenché
3. Vérifier que le nettoyage est suspendu pendant le reset
4. Vérifier que tout est réinitialisé correctement

### Test 4 : Synchronisation périodique

1. Modifier le JSON pendant que l'application tourne
2. Vérifier que les changements sont détectés dans les 5 secondes
3. Vérifier que les nouveaux joueurs sont ajoutés
4. Vérifier que les joueurs inactifs sont retirés

## Migration depuis LogParser

Le système utilise automatiquement `JsonPlayerDataProvider` si disponible, sinon il fallback sur `LogParserPlayerDataProvider`. Pour forcer l'utilisation du JSON :

1. Créer le fichier `player_data.json` dans `%APPDATA%\Amaliassistant\Kikimeter\`
2. Le système le détectera automatiquement
3. Le fallback sera utilisé uniquement si le JSON n'est pas disponible

## Notes importantes

- Le système ne supprime jamais le personnage principal
- Le système respecte la limite de 6 joueurs dans le groupe
- Le système maintient l'ordre de tour pour les joueurs actifs
- Le nettoyage ne se fait que lorsque aucun combat n'est actif (sauf après `OnCombatEnded`)
- La synchronisation périodique est désactivée pendant un combat actif
- Le reset serveur suspend automatiquement le nettoyage et la synchronisation
