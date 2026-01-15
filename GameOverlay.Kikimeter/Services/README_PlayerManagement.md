# Service de Gestion Automatique des Joueurs

## Vue d'ensemble

Ce service gère automatiquement le nettoyage des joueurs après les combats, basé sur les données issues du **polling JSON** comme source de vérité. Il garantit que :

- Les joueurs quittant le combat sont retirés automatiquement
- Les joueurs du groupe (max 6) restent visibles
- Les adversaires détectés lors d'agression sont retirés après le combat
- L'ordre des joueurs dans le Kikimeter et l'Ordre des joueurs reste cohérent
- Le personnage principal est toujours préservé

## Architecture

### Composants principaux

1. **`IPlayerDataProvider`** : Interface pour fournir les données des joueurs depuis le polling JSON
2. **`PlayerManagementService`** : Service principal qui gère le nettoyage et la synchronisation
3. **`LogParserPlayerDataProvider`** : Implémentation actuelle basée sur le LogParser (pont de transition)
4. **`PlayerStats`** : Modèle enrichi avec les propriétés `IsInGroup`, `IsMainCharacter`, `IsActive`, `LastSeenInCombat`

### Flux de données

```
Polling JSON → IPlayerDataProvider → PlayerManagementService → KikimeterWindow
```

## Utilisation

### Intégration dans KikimeterWindow

Le service est automatiquement initialisé dans `StartWatching()` :

```csharp
_playerDataProvider = new LogParserPlayerDataProvider(_logWatcher.Parser);
_playerManagementService = new PlayerManagementService(_playerDataProvider);
```

### Nettoyage automatique après combat

Le nettoyage est déclenché automatiquement dans `OnCombatEnded()` :

```csharp
_playerManagementService.CleanupPlayersAfterCombat(_playersCollection, DateTime.Now);
```

### Synchronisation périodique

Une synchronisation périodique (toutes les 5 secondes) est effectuée dans `UpdateTimer_Tick()` lorsque aucun combat n'est actif :

```csharp
_playerManagementService.SyncPlayersWithJson(_playersCollection);
```

## Règles de nettoyage

### Joueurs conservés

1. **Personnage principal** : Jamais retiré, même s'il n'est plus actif
2. **Joueurs du groupe** : Conservés s'ils font partie du groupe (max 6)
3. **Joueurs actifs** : Conservés s'ils sont actuellement actifs dans un combat

### Joueurs retirés

1. **Adversaires inactifs** : Joueurs non dans le groupe, non actifs, et non vus depuis plus de 30 secondes
2. **Joueurs absents du JSON** : Joueurs non trouvés dans les données JSON et non dans le groupe

### Limite de groupe

- Maximum 6 joueurs dans le groupe
- Si plus de 6 joueurs sont dans le groupe, les 6 plus récents sont conservés
- Le personnage principal est toujours conservé même s'il dépasse la limite

## Migration vers un vrai polling JSON

### Étape 1 : Créer une implémentation JSON

Créer une nouvelle classe qui implémente `IPlayerDataProvider` :

```csharp
public class JsonPollingPlayerDataProvider : IPlayerDataProvider
{
    private readonly string _jsonFilePath;
    private readonly System.Threading.Timer _pollingTimer;
    
    public Dictionary<string, PlayerData> GetCurrentPlayers()
    {
        // Lire le fichier JSON
        // Parser les données
        // Retourner un dictionnaire de PlayerData
    }
    
    public HashSet<string> GetPlayersInCombat()
    {
        // Détecter les joueurs actuellement en combat depuis le JSON
    }
    
    public bool IsCombatActive { get; }
}
```

### Étape 2 : Remplacer le provider

Dans `KikimeterWindow.StartWatching()`, remplacer :

```csharp
// Ancien
_playerDataProvider = new LogParserPlayerDataProvider(_logWatcher.Parser);

// Nouveau
_playerDataProvider = new JsonPollingPlayerDataProvider(jsonFilePath);
```

### Étape 3 : Format JSON attendu

Le format JSON devrait ressembler à :

```json
{
  "players": [
    {
      "id": "123456",
      "name": "NomJoueur",
      "isMainCharacter": true,
      "isInGroup": true,
      "isActive": true,
      "lastSeenInCombat": "2024-01-15T10:30:00"
    }
  ],
  "combatActive": false
}
```

## Propriétés PlayerStats

### IsInGroup
- `true` : Le joueur fait partie du groupe (max 6)
- `false` : Le joueur n'est pas dans le groupe

### IsMainCharacter
- `true` : C'est le personnage principal du joueur
- `false` : Ce n'est pas le personnage principal

### IsActive
- `true` : Le joueur est actuellement actif (dans un combat ou dans le groupe)
- `false` : Le joueur est inactif

### LastSeenInCombat
- Dernière fois que le joueur a été vu dans un combat
- Utilisé pour déterminer si un joueur doit être retiré après la fin d'un combat

## Logs

Le service génère des logs détaillés pour le débogage :

- `PlayerManagementService` : Logs du service principal
- `LogParserPlayerDataProvider` : Logs du provider actuel
- `KikimeterWindow` : Logs de l'intégration

## Tests

Pour tester le nettoyage automatique :

1. Démarrer un combat avec plusieurs joueurs
2. Terminer le combat
3. Vérifier que les adversaires sont retirés automatiquement
4. Vérifier que les joueurs du groupe sont conservés
5. Vérifier que le personnage principal est toujours présent

## Notes importantes

- Le service ne supprime jamais le personnage principal
- Le service respecte la limite de 6 joueurs dans le groupe
- Le service maintient l'ordre de tour pour les joueurs actifs
- Le nettoyage ne se fait que lorsque aucun combat n'est actif (sauf après `OnCombatEnded`)
- La synchronisation périodique est désactivée pendant un combat actif
