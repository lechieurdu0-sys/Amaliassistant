# Résumé du problème : FileSystemWatcher ne fonctionne plus après changement de session Windows

## Contexte de l'application

Application WPF en C# (.NET 8) qui surveille des fichiers de log de jeu (Wakfu) en temps réel :
- `wakfu.log` : contient les statistiques de combat des joueurs
- `wakfu_chat.log` : contient les messages de chat, les loots et les notifications de vente

L'application utilise des `FileSystemWatcher` pour détecter les changements dans ces fichiers et les lire avec `FileStream` en mode `FileShare.ReadWrite`.

## Problème observé

**Symptômes :**
- Les chemins des fichiers de log sont correctement configurés (manuellement, car l'auto-détection ne trouve pas les chemins personnalisés Steam)
- Les fichiers de log existent et sont accessibles (vérifié manuellement)
- Les `FileSystemWatcher` sont créés et activés (`EnableRaisingEvents = true`)
- Mais **aucune donnée n'est lue** : pas de personnages dans les paramètres, pas de données dans le Kikimeter, pas de joueurs/loot affichés
- Le problème est apparu après avoir créé une nouvelle session Windows et supprimé l'ancienne

**Avant le changement de session :** Tout fonctionnait parfaitement (même chemin personnalisé)

**Après le changement de session :** Les watchers ne lisent plus rien, même avec les chemins correctement configurés manuellement

**⚠️ IMPORTANT :** Le problème n'est PAS la détection des chemins - même avec les chemins correctement configurés manuellement, les watchers ne lisent toujours pas les données

## Architecture technique

### Fichiers surveillés

**Chemins recherchés par l'application :**

1. **Steam** :
   - `[DriveRoot]\SteamLibrary\steamapps\common\Wakfu\preferences\logs\wakfu.log` (racine des lecteurs)
   - `Program Files\Steam\steamapps\common\Wakfu\preferences\logs\wakfu.log`
   - `Program Files (x86)\Steam\steamapps\common\Wakfu\preferences\logs\wakfu.log`
   - ❌ **NE CHERCHE PAS dans** `Users\[Username]\Desktop\Jeux\Steam\...` (chemins personnalisés)

2. **Ankama Launcher (Zaap)** :
   - `%AppData%\zaap\gamesLogs\wakfu\logs\wakfu.log` (Roaming AppData)
   - `%AppData%\Wakfu\logs\wakfu.log` (Roaming AppData - fallback)
   - `Program Files (x86)\Ankama\Zaap\gamesLogs\wakfu\logs\wakfu.log`
   - ❌ **NE CHERCHE PAS dans** `%LOCALAPPDATA%\Ankama\Wakfu\game\logs\` (LocalAppData)

**⚠️ PROBLÈME POTENTIEL :**
- Il se peut que les logs Ankama soient dans `%LOCALAPPDATA%\Ankama\Wakfu\game\logs\` (LocalAppData) au lieu de `%AppData%` (Roaming AppData)
- L'application ne cherche **PAS** dans LocalAppData pour Ankama
- Fichiers : `wakfu.log` et `wakfu_chat.log`
- Le jeu écrit constamment dans ces fichiers pendant l'exécution

### Méthode de surveillance

L'application utilise plusieurs services qui utilisent `FileSystemWatcher` :

1. **LogFileWatcher** (pour `wakfu.log`)
   - `FileSystemWatcher` avec `NotifyFilter = Size | LastWrite | FileName`
   - Lecture avec `FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)`
   - Timer périodique pour lire les nouvelles lignes

2. **LootTracker** (pour `wakfu_chat.log` - loots)
   - Même configuration `FileSystemWatcher`
   - Même méthode de lecture (`FileShare.ReadWrite`)

3. **SaleTracker** (pour `wakfu_chat.log` - ventes)
   - Même configuration `FileSystemWatcher`
   - Buffer interne de 64 KB configuré
   - Timer de secours en cas d'échec du watcher

### Code de lecture typique

```csharp
using var reader = new StreamReader(
    new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
);
reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
string? line;
while ((line = reader.ReadLine()) != null)
{
    // Traitement de la ligne
}
_lastPosition = reader.BaseStream.Position;
```

## ⚠️ HYPOTHÈSE PRINCIPALE : FileSystemWatcher bloqué après changement de session Windows

**PROBLÈME IDENTIFIÉ :** Même avec les chemins correctement configurés manuellement, les `FileSystemWatcher` ne déclenchent pas d'événements après un changement de session Windows.

**Chemin réel des logs :** `D:\Users\[Username]\Desktop\Jeux\Steam\steamapps\common\Wakfu\preferences\logs\`

**Vérifications effectuées :**
- ✅ Les fichiers de log existent et sont accessibles
- ✅ Les chemins sont correctement configurés dans l'application (manuellement)
- ✅ Les `FileSystemWatcher` sont créés avec les bons chemins
- ✅ `EnableRaisingEvents = true` est défini
- ❌ Mais aucun événement n'est déclenché et aucune donnée n'est lue

**Le problème :** 
- Le problème n'est PAS la détection des chemins (configurés manuellement avec succès)
- Le problème est que `FileSystemWatcher` ne fonctionne plus après un changement de session Windows
- Les watchers sont créés mais ne déclenchent aucun événement, même quand les fichiers changent

## Hypothèses sur la cause Windows

### 1. Permissions de fichiers
- **Possible** : Les permissions NTFS ont changé avec la nouvelle session
- **Vérification** : L'application peut lire les fichiers (pas d'erreur d'accès), mais peut-être pas les événements du FileSystemWatcher

### 2. FileSystemWatcher et sessions utilisateur
- **Possible** : Le `FileSystemWatcher` peut avoir des problèmes avec les sessions utilisateur Windows
- **Connu** : Les FileSystemWatcher peuvent ne pas fonctionner correctement après certains changements de session
- **Symptôme** : Les watchers sont créés mais ne déclenchent pas d'événements

### 3. Isolation des processus
- **Possible** : Windows isole les processus entre sessions d'une manière qui affecte FileSystemWatcher
- **Symptôme** : Pas d'événements déclenchés même si les fichiers changent

### 4. Buffer interne du FileSystemWatcher
- **Possible** : Le buffer interne peut être saturé ou corrompu
- **Déjà testé** : Augmentation du buffer à 64 KB (pas de changement)

### 5. Compteur d'événements Windows
- **Possible** : Les événements système sont bloqués ou filtrés au niveau du noyau Windows
- **Symptôme** : Aucun événement ne parvient à l'application

## Tentatives de correction déjà effectuées (dans l'application)

1. ✅ Validation automatique des chemins de log
2. ✅ Redémarrage des watchers après mise à jour des chemins
3. ✅ Augmentation de la taille du buffer du FileSystemWatcher (64 KB)
4. ✅ Ajout de timers de secours pour la lecture périodique
5. ✅ Gestion des erreurs et réinitialisation des watchers
6. ✅ Vérification que les watchers sont bien activés (`EnableRaisingEvents = true`)

**Résultat** : Aucune amélioration - les watchers ne déclenchent toujours pas d'événements

## Informations système

- **OS** : Windows 10 (version 10.0.19045)
- **.NET** : .NET 8.0
- **Architecture** : x64
- **Session** : Nouvelle session Windows créée, ancienne supprimée
- **Jeu** : Wakfu (écrit constamment dans les fichiers de log)

## Questions pour résolution

1. **Y a-t-il des paramètres Windows qui peuvent bloquer FileSystemWatcher après un changement de session ?**
   - Services Windows désactivés ?
   - Permissions système modifiées ?
   - Politiques de sécurité ?

2. **Existe-t-il des alternatives à FileSystemWatcher qui fonctionnent mieux avec les sessions Windows ?**
   - `ReadDirectoryChangesW` API native Windows
   - Polling avec `FileInfo.LastWriteTime`
   - Service Windows dédié

3. **Faut-il configurer des permissions spécifiques au niveau système pour FileSystemWatcher ?**
   - Permissions NTFS sur les dossiers surveillés
   - Permissions utilisateur système
   - UAC / Élévation des privilèges

4. **Y a-t-il des limitations connues de FileSystemWatcher avec les fichiers fréquemment modifiés sur Windows 10 ?**
   - Buffer saturé
   - Événements manqués
   - Problèmes avec les sessions utilisateur

5. **Le problème peut-il venir de l'isolation des processus entre sessions ?**
   - Les événements système sont-ils bloqués entre sessions ?
   - Le FileSystemWatcher nécessite-t-il des privilèges système spécifiques ?
   - Les événements sont-ils filtrés par le noyau Windows ?

6. **Peut-on utiliser des timers de polling au lieu de FileSystemWatcher ?**
   - Lire périodiquement les fichiers au lieu de s'appuyer sur les événements
   - Vérifier `FileInfo.LastWriteTime` et `Length` pour détecter les changements

## Logs à vérifier (si disponibles)

- Événements Windows (Event Viewer)
- Logs de sécurité Windows
- Logs d'application (.NET)

## Solutions alternatives à explorer

1. **Polling au lieu de FileSystemWatcher** : Lire périodiquement les fichiers au lieu de s'appuyer sur les événements
2. **Service Windows** : Créer un service Windows pour la surveillance des fichiers
3. **ReadDirectoryChangesW API** : Utiliser directement l'API Windows au lieu de FileSystemWatcher
4. **WMI Events** : Utiliser les événements WMI pour surveiller les fichiers
