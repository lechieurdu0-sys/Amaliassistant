# Guide de Test - Gestion Automatique des Joueurs

## Prérequis

1. Application WPF compilée et fonctionnelle
2. Fichier JSON de test : `%APPDATA%\Amaliassistant\Kikimeter\player_data.json`
3. Accès aux logs de l'application pour vérifier le comportement

## Configuration initiale

### 1. Créer le fichier JSON de test

Copier `player_data.example.json` vers `%APPDATA%\Amaliassistant\Kikimeter\player_data.json` et modifier selon vos besoins.

### 2. Structure de test recommandée

```json
{
  "players": [
    {
      "id": "1",
      "name": "MonPersonnage",
      "isMainCharacter": true,
      "isInGroup": true,
      "isActive": true,
      "lastSeenInCombat": "2026-01-15T10:30:00",
      "playerId": 1
    },
    {
      "id": "2",
      "name": "Ami1",
      "isMainCharacter": false,
      "isInGroup": true,
      "isActive": true,
      "lastSeenInCombat": "2026-01-15T10:30:05",
      "playerId": 2
    },
    {
      "id": "3",
      "name": "Ami2",
      "isMainCharacter": false,
      "isInGroup": true,
      "isActive": true,
      "lastSeenInCombat": "2026-01-15T10:30:10",
      "playerId": 3
    },
    {
      "id": "4",
      "name": "Adversaire1",
      "isMainCharacter": false,
      "isInGroup": false,
      "isActive": false,
      "lastSeenInCombat": "2026-01-15T10:25:00",
      "playerId": 4
    }
  ],
  "combatActive": false,
  "lastUpdate": "2026-01-15T10:30:15",
  "serverName": "wakfu-server-1"
}
```

## Tests à effectuer

### Test 1 : Nettoyage automatique après combat

**Objectif** : Vérifier que les adversaires sont retirés après la fin d'un combat.

**Étapes** :
1. Démarrer l'application
2. Configurer le JSON avec :
   - 3 joueurs du groupe (actifs)
   - 2 adversaires (inactifs, `isInGroup: false`)
   - `combatActive: true`
3. Attendre que le combat soit détecté
4. Modifier le JSON : `combatActive: false`
5. Attendre 5 secondes (synchronisation périodique)

**Résultat attendu** :
- Les 3 joueurs du groupe restent visibles
- Les 2 adversaires sont retirés automatiquement
- Le personnage principal reste visible

**Vérification** :
- Ouvrir les logs de l'application
- Chercher : `"Nettoyage des joueurs après combat terminé"`
- Vérifier : `"X joueurs restants"` (devrait être 3)

### Test 2 : Limite de groupe (6 joueurs max)

**Objectif** : Vérifier que le groupe ne dépasse jamais 6 joueurs.

**Étapes** :
1. Configurer le JSON avec 7 joueurs dans le groupe (`isInGroup: true`)
2. Démarrer l'application
3. Attendre la synchronisation (5 secondes)

**Résultat attendu** :
- Seulement 6 joueurs sont conservés (les plus récents)
- Le personnage principal est toujours présent même s'il est le 7ème
- Le moins récent (non principal) est retiré

**Vérification** :
- Compter les joueurs dans le Kikimeter
- Vérifier les logs : `"Joueur 'X' retiré du groupe (limite de 6 atteinte)"`

### Test 3 : Préservation du personnage principal

**Objectif** : Vérifier que le personnage principal n'est jamais retiré.

**Étapes** :
1. Configurer le JSON avec un personnage principal inactif (`isActive: false`)
2. Démarrer l'application
3. Attendre la synchronisation

**Résultat attendu** :
- Le personnage principal reste visible même s'il est inactif
- Les autres joueurs inactifs sont retirés

**Vérification** :
- Vérifier que le personnage principal est toujours dans le Kikimeter
- Vérifier les logs : aucun message de suppression pour le personnage principal

### Test 4 : Synchronisation périodique

**Objectif** : Vérifier que les changements dans le JSON sont détectés automatiquement.

**Étapes** :
1. Démarrer l'application avec un JSON initial
2. Attendre 5 secondes
3. Modifier le JSON (ajouter un joueur, modifier `isActive`, etc.)
4. Sauvegarder le fichier
5. Attendre 5 secondes (synchronisation)

**Résultat attendu** :
- Les nouveaux joueurs sont ajoutés automatiquement
- Les propriétés des joueurs existants sont mises à jour
- Les joueurs inactifs sont retirés

**Vérification** :
- Vérifier les logs : `"Données JSON mises à jour"`
- Vérifier que les changements sont reflétés dans le Kikimeter

### Test 5 : Fallback LogParser

**Objectif** : Vérifier que le système utilise LogParser si le JSON est absent.

**Étapes** :
1. Supprimer ou renommer `player_data.json`
2. Démarrer l'application
3. Vérifier les logs

**Résultat attendu** :
- Le système détecte l'absence du JSON
- Utilise automatiquement `LogParserPlayerDataProvider`
- Aucune erreur fatale

**Vérification** :
- Vérifier les logs : `"Impossible d'initialiser JsonPlayerDataProvider, utilisation du fallback"`
- Vérifier que l'application fonctionne normalement

### Test 6 : Reset serveur

**Objectif** : Vérifier que le reset serveur suspend le nettoyage.

**Étapes** :
1. Démarrer l'application avec un serveur : `"serverName": "wakfu-server-1"`
2. Modifier le JSON : `"serverName": "wakfu-server-2"`
3. Sauvegarder le fichier
4. Attendre la détection (1 seconde)
5. Vérifier que `ResetDisplayFromLoot()` est appelé

**Résultat attendu** :
- Le changement de serveur est détecté
- Le nettoyage est suspendu (`BeginReset()`)
- La collection est vidée
- Le nettoyage est réactivé (`EndReset()`)

**Vérification** :
- Vérifier les logs : `"Changement de serveur détecté"`
- Vérifier les logs : `"Reset serveur détecté, nettoyage et synchronisation suspendus"`
- Vérifier les logs : `"Reset serveur terminé, nettoyage et synchronisation réactivés"`

### Test 7 : Changement de personnage principal

**Objectif** : Vérifier que le changement de personnage principal est géré correctement.

**Étapes** :
1. Configurer le JSON avec un personnage principal : `"MonPersonnage"` (`isMainCharacter: true`)
2. Démarrer l'application
3. Modifier le JSON : changer le personnage principal vers `"Ami1"`
4. Sauvegarder le fichier
5. Attendre la synchronisation

**Résultat attendu** :
- L'ancien personnage principal devient un joueur normal
- Le nouveau personnage principal est préservé
- Aucun joueur n'est perdu

**Vérification** :
- Vérifier que les deux joueurs sont toujours présents
- Vérifier que seul le nouveau a `isMainCharacter: true`

### Test 8 : Multi-personnages

**Objectif** : Vérifier que plusieurs personnages peuvent être gérés.

**Étapes** :
1. Configurer le JSON avec plusieurs personnages dans le groupe
2. Démarrer l'application
3. Vérifier que tous sont visibles

**Résultat attendu** :
- Tous les personnages du groupe sont visibles (max 6)
- L'ordre de tour est maintenu
- Les statistiques sont correctes

**Vérification** :
- Compter les joueurs dans le Kikimeter
- Vérifier l'ordre des joueurs

### Test 9 : Joueurs quittant le combat

**Objectif** : Vérifier que les joueurs quittant le combat sont retirés après 30 secondes.

**Étapes** :
1. Configurer le JSON avec des joueurs actifs dans un combat
2. Démarrer l'application
3. Modifier le JSON : mettre `isActive: false` et `lastSeenInCombat` à une date ancienne (> 30s)
4. Sauvegarder le fichier
5. Attendre 35 secondes

**Résultat attendu** :
- Les joueurs inactifs (hors groupe) sont retirés après 30 secondes
- Les joueurs du groupe restent visibles même s'ils sont inactifs

**Vérification** :
- Vérifier les logs : `"Adversaire 'X' à retirer (inactif depuis Xs)"`
- Vérifier que les joueurs du groupe ne sont pas retirés

### Test 10 : Gestion des erreurs JSON

**Objectif** : Vérifier que les erreurs JSON sont gérées gracieusement.

**Étapes** :
1. Créer un JSON invalide (syntaxe incorrecte)
2. Démarrer l'application
3. Vérifier les logs

**Résultat attendu** :
- L'erreur est loggée
- Le système utilise le fallback LogParser
- Aucune erreur fatale

**Vérification** :
- Vérifier les logs : `"Erreur de parsing JSON"`
- Vérifier que l'application continue de fonctionner

## Simulation de scénarios

### Scénario 1 : Combat complet

```json
// Étape 1 : Avant le combat
{
  "combatActive": false,
  "players": [
    {"name": "MonPersonnage", "isMainCharacter": true, "isInGroup": true, "isActive": true},
    {"name": "Ami1", "isInGroup": true, "isActive": true},
    {"name": "Ami2", "isInGroup": true, "isActive": true}
  ]
}

// Étape 2 : Début du combat
{
  "combatActive": true,
  "players": [
    // ... joueurs du groupe ...
    {"name": "Adversaire1", "isInGroup": false, "isActive": true},
    {"name": "Adversaire2", "isInGroup": false, "isActive": true}
  ]
}

// Étape 3 : Fin du combat
{
  "combatActive": false,
  "players": [
    // ... joueurs du groupe restent actifs ...
    {"name": "Adversaire1", "isInGroup": false, "isActive": false, "lastSeenInCombat": "2026-01-15T10:25:00"},
    {"name": "Adversaire2", "isInGroup": false, "isActive": false, "lastSeenInCombat": "2026-01-15T10:25:00"}
  ]
}
```

### Scénario 2 : Changement de serveur

```json
// Avant
{
  "serverName": "wakfu-server-1",
  "players": [...]
}

// Après
{
  "serverName": "wakfu-server-2",
  "players": []  // Liste vidée par le reset
}
```

## Vérification des logs

### Logs importants à surveiller

- `JsonPlayerDataProvider` : Polling et lecture JSON
- `PlayerManagementService` : Nettoyage et synchronisation
- `KikimeterWindow` : Intégration et événements

### Commandes de recherche dans les logs

```bash
# Vérifier le polling JSON
grep "JsonPlayerDataProvider" logs.txt

# Vérifier le nettoyage
grep "Nettoyage des joueurs" logs.txt

# Vérifier le reset serveur
grep "Changement de serveur" logs.txt

# Vérifier les erreurs
grep "ERREUR\|ERROR" logs.txt
```

## Checklist de validation

- [ ] Test 1 : Nettoyage automatique après combat
- [ ] Test 2 : Limite de groupe (6 joueurs max)
- [ ] Test 3 : Préservation du personnage principal
- [ ] Test 4 : Synchronisation périodique
- [ ] Test 5 : Fallback LogParser
- [ ] Test 6 : Reset serveur
- [ ] Test 7 : Changement de personnage principal
- [ ] Test 8 : Multi-personnages
- [ ] Test 9 : Joueurs quittant le combat
- [ ] Test 10 : Gestion des erreurs JSON

## Dépannage

### Le JSON n'est pas détecté

- Vérifier le chemin : `%APPDATA%\Amaliassistant\Kikimeter\player_data.json`
- Vérifier les permissions du fichier
- Vérifier les logs pour les erreurs de lecture

### Les joueurs ne sont pas synchronisés

- Vérifier que `combatActive` est `false` (synchronisation désactivée pendant combat)
- Vérifier que le fichier JSON est bien sauvegardé
- Attendre 5 secondes pour la synchronisation périodique

### Le reset serveur ne fonctionne pas

- Vérifier que `serverName` est présent dans le JSON
- Vérifier que `ResetDisplayFromLoot()` est appelé
- Vérifier les logs pour `BeginReset()` / `EndReset()`
