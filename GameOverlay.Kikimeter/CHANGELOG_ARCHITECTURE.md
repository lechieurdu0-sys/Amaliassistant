# Changelog - Améliorations Architecturales

## Version actuelle - Corrections Critiques

### 1. ✅ Création automatique du JSON

**Problème** : Le fichier `player_data.json` n'était pas créé automatiquement à l'initialisation.

**Solution** :
- Création de `PlayerDataJsonInitializer.cs` pour initialiser automatiquement le JSON
- Le fichier est créé dans `%APPDATA%\Amaliassistant\Kikimeter\player_data.json`
- Ne bloque jamais le démarrage si le fichier est manquant ou inaccessible
- Gestion gracieuse des erreurs (fichier corrompu, accès refusé, etc.)

**Fichiers modifiés** :
- `Services/PlayerDataJsonInitializer.cs` (nouveau)
- `Services/JsonPlayerDataProvider.cs` (appel à l'initialisation)

### 2. ✅ Séparation stricte Loot / Kikimeter

**Problème** : Risque de confusion entre le système de loot et la présence des joueurs dans le Kikimeter.

**Solution** :
- Documentation claire dans `LootCharacterDetector.RegisterCombatPlayers()` : méthode UNIDIRECTIONNELLE
- Le loot ne crée JAMAIS de joueurs dans le Kikimeter
- Le loot met uniquement à jour les statistiques d'affichage
- `UpdateLootWindowCombatPlayers()` est documenté comme unidirectionnel (Kikimeter → Loot)

**Règle d'or** :
> Le loot ne doit JAMAIS être une source de vérité pour la présence d'un joueur dans le Kikimeter.

**Fichiers modifiés** :
- `Services/LootCharacterDetector.cs` (documentation)
- `KikimeterWindow.cs` (documentation de `UpdateLootWindowCombatPlayers()`)

### 3. ✅ Protection du reset serveur

**Problème** : Le nettoyage automatique pouvait interférer avec le reset serveur.

**Solution** :
- Ajout de flags `_isResetInProgress` et `_isInitialized`
- Le nettoyage est suspendu pendant le reset
- La synchronisation périodique est suspendue pendant le reset
- Protection dans `OnCombatEnded()` et `UpdateTimer_Tick()`

**Fichiers modifiés** :
- `KikimeterWindow.cs` (flags de protection)
- `KikimeterWindow.Reset.cs` (gestion des flags)

### 4. ✅ Amélioration des initialisations

**Problème** : Risque d'effets de bord lors des initialisations multiples.

**Solution** :
- Flag `_isInitializing` pour protéger contre les initialisations multiples
- Ordre d'initialisation déterministe :
  1. Initialisation du JSON (ne bloque jamais)
  2. Création du LogFileWatcher
  3. Création du PlayerDataProvider
  4. Création du PlayerManagementService
  5. Abonnement aux événements
  6. Démarrage du timer
- Protection avec try/finally pour garantir la réinitialisation des flags

**Fichiers modifiés** :
- `KikimeterWindow.cs` (ordre d'initialisation et flags)

### 5. ✅ Gestion robuste des erreurs JSON

**Problème** : Le système pouvait planter si le JSON était vide ou invalide.

**Solution** :
- Gestion des fichiers vides (retourne un JSON par défaut)
- Gestion des fichiers corrompus (retourne un JSON par défaut)
- Gestion des erreurs de désérialisation (retourne un JSON par défaut)
- Le système continue de fonctionner même avec un JSON invalide

**Fichiers modifiés** :
- `Services/JsonPlayerDataProvider.cs` (gestion des erreurs améliorée)

## Architecture finale

### Flux de données

```
Combat détecté → LogParser → PlayerAdded event → KikimeterWindow
                                                      ↓
                                            JSON (source de vérité)
                                                      ↓
                                            PlayerManagementService
                                                      ↓
                                            Nettoyage automatique
                                                      ↓
                                            UpdateLootWindowCombatPlayers (unidirectionnel)
                                                      ↓
                                            LootWindow (affichage uniquement)
```

### Règles strictes

1. **JSON = Source de vérité** : La présence des joueurs vient uniquement du JSON ou de la détection de combat
2. **Loot = Stats uniquement** : Le loot ne crée jamais de joueurs, ne maintient jamais la présence
3. **Reset serveur protégé** : Aucun nettoyage ou synchronisation pendant le reset
4. **Initialisation déterministe** : Ordre fixe, flags de protection, gestion d'erreurs gracieuse

## Tests de validation

### Test 1 : Création automatique du JSON
- ✅ Le fichier est créé automatiquement au démarrage
- ✅ Le système fonctionne même si le fichier est vide
- ✅ Le système fonctionne même si le fichier est corrompu

### Test 2 : Séparation Loot / Kikimeter
- ✅ Le loot ne crée jamais de joueurs dans le Kikimeter
- ✅ `UpdateLootWindowCombatPlayers()` est unidirectionnel
- ✅ `RegisterCombatPlayers()` ne remonte jamais vers le Kikimeter

### Test 3 : Protection du reset serveur
- ✅ Le nettoyage est suspendu pendant le reset
- ✅ La synchronisation est suspendue pendant le reset
- ✅ Tout est réactivé après le reset

### Test 4 : Initialisations
- ✅ Pas d'effets de bord lors des initialisations multiples
- ✅ Ordre d'initialisation déterministe
- ✅ Gestion gracieuse des erreurs

## Prochaines étapes

- [ ] Tests d'intégration complets
- [ ] Validation avec les amis testeurs
- [ ] Création de l'installateur (local uniquement, pas sur GitHub)
