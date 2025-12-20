using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using GameOverlay.Kikimeter.Models;
using Newtonsoft.Json;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service pour détecter les 6 personnages les plus récents présents dans les combats
/// et gérer leur configuration (affichage/masquage des loots)
/// </summary>
public class LootCharacterDetector : IDisposable
{
    private static readonly object ConfigInitLock = new();
    private readonly string _logFilePath;
    private readonly string _kikimeterLogPath; // Chemin vers wakfu.log pour détecter les joueurs
    private readonly string _configFilePath;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly FileSystemWatcher? _kikimeterFileWatcher;
    private long _lastPosition = 0;
    private long _lastKikimeterPosition = 0;
    private readonly object _serverLock = new();
    private string _currentServer = string.Empty;
    private readonly object _scanLock = new();
    private DateTime _lastScanTimestamp = DateTime.MinValue;
    private volatile bool _suppressServerNotifications;
    public static void EnsureConfigFileExists()
    {
        lock (ConfigInitLock)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configDir = Path.Combine(appDataPath, "Amaliassistant", "Loot");
                Directory.CreateDirectory(configDir);

                var configPath = Path.Combine(configDir, "loot_characters.json");
                if (!File.Exists(configPath))
                {
                    var config = new LootCharacterConfig
                    {
                        LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(configPath, json, Encoding.UTF8);
                    Logger.Info("LootCharacterDetector", $"Configuration initiale créée ({configPath})");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("LootCharacterDetector", $"Impossible de créer le fichier loot_characters.json: {ex.Message}");
            }
        }
    }

    
    // Regex pour détecter les joueurs (identique à LogParser du Kikimeter)
    // Utilise isControlledByAI=false pour distinguer les joueurs des monstres
    private static readonly Regex PlayerJoinRegex = new Regex(
        @".*?\[_FL_\].*?fightId=\d+\s+(.+?)\s+breed\s*:\s*(\d+)\s+\[(\d+)\]\s+isControlledByAI=(true|false)",
        RegexOptions.Compiled);
    private static readonly Regex ServerConnectionRegex = new Regex(
        @"Connexion au proxy\s*:(?<host>wakfu-[^:\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Regex pour détecter les personnages dans wakfu_chat.log (fallback uniquement)
    private static readonly Regex JoinedGroupRegex = new Regex(
        @"\[Information \(jeu\)\]\s+(.+?)\s+a rejoint le groupe",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    
    // Regex pour détecter quand un joueur quitte le groupe
    private static readonly Regex LeftGroupRegex = new Regex(
        @"\[Information \(jeu\)\]\s+(.+?)\s+a quitté le groupe",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    
    // Les 6 personnages les plus récents détectés (sans "Vous")
    private readonly Dictionary<string, DateTime> _recentCharacters = new(StringComparer.OrdinalIgnoreCase);
    // Personnages actuellement dans le groupe (pour ne pas les retirer s'ils quittent le combat)
    private readonly HashSet<string> _playersInGroup = new(StringComparer.OrdinalIgnoreCase);
    // Personnages actuellement dans le combat
    private readonly HashSet<string> _playersInCombat = new(StringComparer.OrdinalIgnoreCase);
    private const int MAX_CHARACTERS = 6;
    private const int RECENT_LOG_TAIL_BYTES = 200_000;
    private const int ScanThrottleMilliseconds = 200;
    private const int ServerContextBacktrackBytes = 128_000;
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;
    
    public event EventHandler<List<string>>? CharactersChanged;
    public event EventHandler<string>? MainCharacterDetected;
    public event EventHandler<ServerChangeDetectedEventArgs>? ServerChanged;
    
    public LootCharacterDetector(string logFilePath, string? kikimeterLogPath = null)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        
        // Chemin vers wakfu.log pour détecter les joueurs (même logique que Kikimeter)
        // Si non fourni, essayer de le déduire depuis le chemin du chat log
        if (string.IsNullOrEmpty(kikimeterLogPath))
        {
            var chatLogDir = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(chatLogDir))
            {
                _kikimeterLogPath = Path.Combine(chatLogDir, "wakfu.log");
            }
            else
            {
                _kikimeterLogPath = string.Empty;
            }
        }
        else
        {
            _kikimeterLogPath = kikimeterLogPath;
        }
        
        // Chemin du fichier de configuration JSON
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "Amaliassistant", "Loot");
        Directory.CreateDirectory(configDir);
        _configFilePath = Path.Combine(configDir, "loot_characters.json");

        // Vérifier si le fichier existe déjà avant de le créer
        bool fileAlreadyExists = File.Exists(_configFilePath);
        
        if (!fileAlreadyExists)
        {
            // Si le fichier n'existe pas, créer un fichier vide et tronquer les logs (première installation)
            Logger.Info("LootCharacterDetector", $"Fichier de configuration non trouvé, création d'un nouveau fichier vide (troncature des logs pour éviter les anciens personnages)");
            ResetStoredCharactersWithLogTruncation();
            // Marquer la première exécution
            MarkAppRun();
        }
        else
        {
            // Si le fichier existe, vérifier si les logs sont récents (déconnexion) ou anciens (réinstallation)
            var config = LoadConfig();
            bool shouldTruncate = ShouldTruncateLogs(config);
            
            if (shouldTruncate)
            {
                // Les logs sont anciens (réinstallation) → tronquer pour éviter les anciens personnages
                Logger.Info("LootCharacterDetector", $"Logs détectés comme anciens (réinstallation), troncature pour éviter les anciens personnages");
                ResetStoredCharactersWithLogTruncation();
            }
            else
            {
                // Les logs sont récents (déconnexion normale) → rehydrater pour ne pas perdre les personnages
                Logger.Info("LootCharacterDetector", $"Logs détectés comme récents (déconnexion normale), rechargement des personnages depuis les logs");
                ResetStoredCharacters();
                InitializeLogPositions(initialLoad: true);
                RehydrateCharactersAfterReset();
            }
            
            // Marquer cette exécution
            MarkAppRun();
        }
        
        // Surveiller le fichier pour les nouveaux personnages
        _fileWatcher = CreateWatcher(_logFilePath);
        _kikimeterFileWatcher = CreateWatcher(_kikimeterLogPath);

        ManualScan();
        
        Logger.Info("LootCharacterDetector", $"LootCharacterDetector initialisé pour: {_logFilePath}");
    }
    
    private static string NormalizeCharacterName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
    
    private FileSystemWatcher? CreateWatcher(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (directory == null || string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite
            };

            watcher.Changed += OnLogFileChanged;
            watcher.EnableRaisingEvents = true;

            Logger.Debug("LootCharacterDetector", $"FileSystemWatcher actif sur {filePath}");

            return watcher;
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Erreur lors de la création du FileSystemWatcher pour {filePath}: {ex.Message}");
            return null;
        }
    }
    
    private void OnLogFileChanged(object sender, FileSystemEventArgs e)
    {
        System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => ScanNewCharacters());
    }
    
    private void ScanExistingCharacters()
    {
        // Priorité : utiliser wakfu.log pour détecter les joueurs (même logique que Kikimeter)
        if (!string.IsNullOrEmpty(_kikimeterLogPath) && File.Exists(_kikimeterLogPath))
        {
            ScanPlayersFromKikimeterLog(_kikimeterLogPath);
        }
        
        // Fallback : utiliser wakfu_chat.log pour détecter les personnages
        if (File.Exists(_logFilePath))
        {
            try
            {
                using var reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    DetectCharacterInChatLogLine(line, DateTime.Now);
                }
                
                _lastPosition = reader.BaseStream.Position;
            }
            catch (Exception ex)
            {
                Logger.Error("LootCharacterDetector", $"Erreur lors du scan initial: {ex.Message}");
            }
        }
        
        UpdateRecentCharacters();
    }

    private void PerformInitialScan()
    {
        bool changed = false;

        if (!string.IsNullOrEmpty(_kikimeterLogPath) && File.Exists(_kikimeterLogPath))
        {
            try
            {
                using var stream = new FileStream(_kikimeterLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    HandleServerChangeLine(line);
                    
                    // Détecter la fin de combat
                    if (line.Contains("[FIGHT] End fight", StringComparison.Ordinal) || 
                        line.Contains("Combat terminé", StringComparison.Ordinal) || 
                        line.Contains("NetInFight Removed", StringComparison.Ordinal))
                    {
                        Logger.Info("LootCharacterDetector", "Fin de combat détectée dans PerformInitialScan");
                        // Appeler RegisterCombatPlayers avec une liste vide pour retirer les joueurs qui ne sont plus dans le combat
                        RegisterCombatPlayers(Array.Empty<string>());
                        continue;
                    }
                    
                    if (line.Contains("join the fight at") && line.Contains("isControlledByAI=false"))
                    {
                        var match = PlayerJoinRegex.Match(line);
                        if (match.Success)
                        {
                            string playerName = NormalizeCharacterName(match.Groups[1].Value);
                            if (!string.IsNullOrEmpty(playerName) && !string.Equals(playerName, "Vous", StringComparison.OrdinalIgnoreCase))
                            {
                                _recentCharacters[playerName] = DateTime.Now;
                                _playersInCombat.Add(playerName); // Marquer comme étant dans le combat
                                Logger.Debug("LootCharacterDetector", $"Détection initiale (wakfu.log): {playerName}");
                                changed = true;
                            }
                        }
                    }
                }
                _lastKikimeterPosition = stream.Position;
            }
            catch (Exception ex)
            {
                Logger.Error("LootCharacterDetector", $"Erreur lors du scan initial du log kikimeter: {ex.Message}");
                _lastKikimeterPosition = 0;
            }
        }
        else
        {
            _lastKikimeterPosition = 0;
        }

        if (File.Exists(_logFilePath))
        {
            try
            {
                using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    HandleServerChangeLine(line);
                    if (DetectCharacterInChatLogLine(line, DateTime.Now))
                    {
                        changed = true;
                    }
                }
                _lastPosition = stream.Position;
            }
            catch (Exception ex)
            {
                Logger.Error("LootCharacterDetector", $"Erreur lors du scan initial du chat: {ex.Message}");
                _lastPosition = 0;
            }
        }
        else
        {
            _lastPosition = 0;
        }

        if (changed)
        {
            Logger.Info("LootCharacterDetector", "Nouveaux personnages détectés, mise à jour de la liste");
            UpdateRecentCharacters();
        }
    }
    
    private void ScanPlayersFromKikimeterLog(string logPath)
    {
        try
        {
            using var reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                HandleServerChangeLine(line);
                
                // Détecter la fin de combat
                if (line.Contains("[FIGHT] End fight", StringComparison.Ordinal) || 
                    line.Contains("Combat terminé", StringComparison.Ordinal) || 
                    line.Contains("NetInFight Removed", StringComparison.Ordinal))
                {
                    Logger.Info("LootCharacterDetector", "Fin de combat détectée dans ScanPlayersFromKikimeterLog");
                    // Appeler RegisterCombatPlayers avec une liste vide pour retirer les joueurs qui ne sont plus dans le combat
                    RegisterCombatPlayers(Array.Empty<string>());
                    continue;
                }
                
                // Utiliser la même logique que LogParser du Kikimeter
                if (line.Contains("join the fight at") && line.Contains("isControlledByAI=false"))
                {
                    var match = PlayerJoinRegex.Match(line);
                    if (match.Success)
                    {
                        string playerName = NormalizeCharacterName(match.Groups[1].Value);
                        if (!string.IsNullOrEmpty(playerName) && !string.Equals(playerName, "Vous", StringComparison.OrdinalIgnoreCase))
                        {
                            _recentCharacters[playerName] = DateTime.Now;
                            _playersInCombat.Add(playerName); // Marquer comme étant dans le combat
                            Logger.Debug("LootCharacterDetector", $"Détection wakfu.log: {playerName}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Erreur lors du scan du log Kikimeter: {ex.Message}");
        }
    }
    
    private void ScanNewCharacters()
    {
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastScanTimestamp).TotalMilliseconds < ScanThrottleMilliseconds)
        {
            return;
        }

        if (!Monitor.TryEnter(_scanLock))
        {
            return;
        }

        try
        {
            _lastScanTimestamp = nowUtc;

            // Priorité : scanner wakfu.log pour les nouveaux joueurs
            if (!string.IsNullOrEmpty(_kikimeterLogPath) && File.Exists(_kikimeterLogPath))
            {
                try
                {
                    var fileInfo = new FileInfo(_kikimeterLogPath);

                    if (fileInfo.Length < _lastKikimeterPosition)
                    {
                        Logger.Info("LootCharacterDetector", "Troncature détectée sur wakfu.log, reprise depuis le début.");
                        _lastKikimeterPosition = 0;
                        ReplayServerContextFromTail();
                    }

                    if (fileInfo.Length > _lastKikimeterPosition)
                    {
                        using var reader = new StreamReader(new FileStream(_kikimeterLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                        reader.BaseStream.Seek(_lastKikimeterPosition, SeekOrigin.Begin);

                        if (_lastKikimeterPosition > 0)
                        {
                            reader.ReadLine(); // Ignorer la ligne partielle
                        }
                        
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            HandleServerChangeLine(line);
                            
                            // Détecter la fin de combat
                            if (line.Contains("[FIGHT] End fight", StringComparison.Ordinal) || 
                                line.Contains("Combat terminé", StringComparison.Ordinal) || 
                                line.Contains("NetInFight Removed", StringComparison.Ordinal))
                            {
                                Logger.Info("LootCharacterDetector", "Fin de combat détectée, nettoyage des joueurs qui ne sont plus dans le combat");
                                // Appeler RegisterCombatPlayers avec une liste vide pour retirer les joueurs qui ne sont plus dans le combat
                                RegisterCombatPlayers(Array.Empty<string>());
                                continue;
                            }
                            
                            if (line.Contains("join the fight at") && line.Contains("isControlledByAI=false"))
                            {
                                var match = PlayerJoinRegex.Match(line);
                                if (match.Success)
                                {
                                    string playerName = NormalizeCharacterName(match.Groups[1].Value);
                                    if (!string.IsNullOrEmpty(playerName) && !string.Equals(playerName, "Vous", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _recentCharacters[playerName] = DateTime.Now;
                                        _playersInCombat.Add(playerName); // Marquer comme étant dans le combat
                                        Logger.Debug("LootCharacterDetector", $"Nouveau joueur détecté dans le combat (wakfu.log): {playerName}");
                                    }
                                }
                            }
                        }

                        _lastKikimeterPosition = reader.BaseStream.Position;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("LootCharacterDetector", $"Erreur lors du scan du log Kikimeter: {ex.Message}");
                }
            }
            
            // Fallback : scanner wakfu_chat.log
            if (File.Exists(_logFilePath))
            {
                try
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length < _lastPosition)
                    {
                        Logger.Info("LootCharacterDetector", "Troncature détectée sur wakfu_chat.log, reprise depuis le début.");
                        _lastPosition = 0;
                    }

                    using var reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
                    
                    if (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            HandleServerChangeLine(line);
                            DetectCharacterInChatLogLine(line, DateTime.Now);
                        }

                        _lastPosition = reader.BaseStream.Position;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("LootCharacterDetector", $"Erreur lors du scan: {ex.Message}");
                }
            }
            
            try
            {
                UpdateRecentCharacters();
            }
            catch (Exception updateEx)
            {
                Logger.Error("LootCharacterDetector", $"Erreur lors de UpdateRecentCharacters: {updateEx.Message}");
            }
        }
        finally
        {
            Monitor.Exit(_scanLock);
        }
    }
    
    private void ReplayServerContextFromTail()
    {
        if (string.IsNullOrEmpty(_kikimeterLogPath) || !File.Exists(_kikimeterLogPath))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(_kikimeterLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = stream.Length;
            if (length == 0)
            {
                return;
            }

            var startPosition = Math.Max(0, length - ServerContextBacktrackBytes);
            stream.Seek(startPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(stream);
            if (startPosition > 0)
            {
                reader.ReadLine(); // ignorer la ligne partielle
            }

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                HandleServerChangeLine(line);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("LootCharacterDetector", $"Impossible de rejouer le contexte serveur: {ex.Message}");
        }
    }

    private bool DetectCharacterInChatLogLine(string line, DateTime timestamp)
    {
        bool found = false;
        
        // 1. "a rejoint le groupe"
        var match = JoinedGroupRegex.Match(line);
        if (match.Success)
        {
            string characterName = NormalizeCharacterName(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(characterName) && !string.Equals(characterName, "Vous", StringComparison.OrdinalIgnoreCase))
            {
                _recentCharacters[characterName] = timestamp;
                _playersInGroup.Add(characterName); // Marquer comme étant dans le groupe
                Logger.Debug("LootCharacterDetector", $"Personnage a rejoint le groupe: {characterName}");
                found = true;
            }
            return found;
        }
        
        // 2. "a quitté le groupe"
        match = LeftGroupRegex.Match(line);
        if (match.Success)
        {
            string characterName = NormalizeCharacterName(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(characterName) && !string.Equals(characterName, "Vous", StringComparison.OrdinalIgnoreCase))
            {
                _playersInGroup.Remove(characterName); // Retirer du groupe
                Logger.Debug("LootCharacterDetector", $"Personnage a quitté le groupe: {characterName}");
                
                // Si le joueur n'est pas dans le combat, le retirer aussi de la liste
                if (!_playersInCombat.Contains(characterName))
                {
                    _recentCharacters.Remove(characterName);
                    Logger.Info("LootCharacterDetector", $"Personnage retiré (pas dans le groupe ni le combat): {characterName}");
                    UpdateRecentCharacters(); // Mettre à jour la liste
                }
                found = true;
            }
        }
        
        return found;
    }
    
    private void UpdateRecentCharacters()
    {
        // Garder seulement les 6 plus récents (triés par date, plus récent en dernier)
        var sortedCharacters = _recentCharacters
            .OrderByDescending(kvp => kvp.Value)
            .Take(MAX_CHARACTERS)
            .Select(kvp => kvp.Key)
            .ToList();
        
        var config = LoadConfig();
        var manualCharacters = new HashSet<string>(
            config.ManualCharacters ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        bool listChanged = false;

        // Ajouter les personnages détectés récemment
        foreach (var character in sortedCharacters)
        {
            if (!config.Characters.ContainsKey(character))
            {
                config.Characters[character] = true;
                listChanged = true;
            }
            // S'assurer que le personnage principal détecté reste visible
            else if (string.Equals(character, config.MainCharacter, StringComparison.OrdinalIgnoreCase))
            {
                // Forcer la visibilité à true pour le personnage principal
                if (!config.Characters[character])
                {
                    config.Characters[character] = true;
                    listChanged = true;
                    Logger.Info("LootCharacterDetector", $"Personnage principal {character} forcé à visible=true dans UpdateRecentCharacters");
                }
            }
        }

        // S'assurer que les personnages ajoutés manuellement restent présents
        foreach (var manualCharacter in manualCharacters)
            {
            if (!string.IsNullOrWhiteSpace(manualCharacter) && !config.Characters.ContainsKey(manualCharacter))
            {
                config.Characters[manualCharacter] = true;
                listChanged = true;
            }
            }
            
        // Conserver le personnage principal même s'il n'est plus détecté automatiquement
        // ET s'assurer qu'il est TOUJOURS visible (coché) - vérifier à chaque fois
        if (!string.IsNullOrWhiteSpace(config.MainCharacter))
        {
            if (!config.Characters.ContainsKey(config.MainCharacter))
        {
            config.Characters[config.MainCharacter] = true;
            listChanged = true;
                Logger.Info("LootCharacterDetector", $"Personnage principal {config.MainCharacter} ajouté avec visible=true");
            }
            else
            {
                // TOUJOURS forcer la visibilité à true pour le personnage principal, même s'il existe déjà
                var wasVisible = config.Characters[config.MainCharacter];
                config.Characters[config.MainCharacter] = true;
                if (!wasVisible)
                {
                    listChanged = true;
                    Logger.Info("LootCharacterDetector", $"Personnage principal {config.MainCharacter} forcé à visible=true (était {wasVisible})");
                }
            }
        }

        // NE PAS retirer les personnages de la config même s'ils ne sont plus dans _recentCharacters
        // Cela permet de conserver les personnages détectés précédemment pour qu'ils restent visibles
        // dans les paramètres même après une déconnexion
        // Les personnages seront seulement retirés s'ils quittent explicitement le groupe ET le combat
        // (géré par DetectCharacterInChatLogLine et RegisterCombatPlayers)

        if (listChanged)
        {
            config.LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveConfig(config);
        }
            
        // Toujours déclencher l'événement pour mettre à jour les UI même si rien n'a changé
        // (au cas où SettingsWindow serait ouvert et aurait besoin de se synchroniser)
            var characterList = config.Characters.Keys.ToList();
            CharactersChanged?.Invoke(this, characterList);
            
        if (listChanged)
        {
            Logger.Info("LootCharacterDetector", $"Liste des personnages mise à jour: {string.Join(", ", characterList)}");
        }
        else
        {
            Logger.Debug("LootCharacterDetector", $"Liste des personnages vérifiée (aucun changement): {string.Join(", ", characterList)}");
        }
    }

    public bool AddManualCharacter(string characterName, bool setAsMain = false)
    {
        characterName = NormalizeCharacterName(characterName);
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return false;
        }

        var config = LoadConfig();
        bool changed = false;

        if (!config.Characters.ContainsKey(characterName))
        {
            config.Characters[characterName] = true;
            changed = true;
        }

        if (config.ManualCharacters == null)
        {
            config.ManualCharacters = new List<string>();
        }

        if (!config.ManualCharacters.Any(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase)))
        {
            config.ManualCharacters.Add(characterName);
            changed = true;
        }

        if (changed)
        {
            config.LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveConfig(config);
            CharactersChanged?.Invoke(this, config.Characters.Keys.ToList());
            Logger.Info("LootCharacterDetector", $"Personnage manuel ajouté: {characterName}");
        }

        if (setAsMain)
        {
            SetMainCharacter(characterName);
            return true;
        }

        return changed;
    }
    
    private LootCharacterConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonConvert.DeserializeObject<LootCharacterConfig>(json);
                if (config != null)
                {
                    Logger.Debug("LootCharacterDetector", $"Configuration chargée depuis {_configFilePath}: {config.Characters.Count} personnages");
                    return config;
                }
            }
            else
            {
                Logger.Info("LootCharacterDetector", $"Fichier de configuration non trouvé: {_configFilePath}, utilisation de la configuration par défaut");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Erreur lors du chargement de la config: {ex.Message}");
        }
        
        // Configuration par défaut
        var defaultConfig = new LootCharacterConfig();
        Logger.Debug("LootCharacterDetector", "Utilisation de la configuration par défaut (vide)");
        return defaultConfig;
    }
    
    private void SaveConfig(LootCharacterConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_configFilePath, json);
            Logger.Info("LootCharacterDetector", $"Configuration sauvegardée ({_configFilePath})");
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Erreur lors de la sauvegarde de la config: {ex.Message}");
            try
            {
                using var stream = File.Create(_configFilePath);
                Logger.Warning("LootCharacterDetector", $"Fichier créé manuellement après échec de sauvegarde: {_configFilePath}");
            }
            catch (Exception inner)
            {
                Logger.Error("LootCharacterDetector", $"Echec de création manuelle de {_configFilePath}: {inner.Message}");
            }
        }
    }
    
    public LootCharacterConfig GetConfig()
    {
        return LoadConfig();
    }
    
    /// <summary>
    /// Détermine si les logs doivent être tronqués en comparant leur date de modification avec la dernière exécution
    /// </summary>
    private bool ShouldTruncateLogs(LootCharacterConfig config)
    {
        try
        {
            // Si pas de timestamp de dernière exécution, considérer comme réinstallation
            if (string.IsNullOrWhiteSpace(config.LastAppRun))
            {
                Logger.Info("LootCharacterDetector", "Pas de timestamp LastAppRun trouvé, considéré comme réinstallation");
                return true;
            }
            
            // Parser le timestamp de dernière exécution
            if (!DateTime.TryParse(config.LastAppRun, out DateTime lastAppRun))
            {
                Logger.Warning("LootCharacterDetector", $"Impossible de parser LastAppRun: {config.LastAppRun}");
                return true; // En cas de doute, tronquer pour sécurité
            }
            
            // Vérifier la date de modification des logs
            DateTime? oldestLogDate = null;
            
            // Vérifier wakfu_chat.log
            if (File.Exists(_logFilePath))
            {
                var chatLogInfo = new FileInfo(_logFilePath);
                if (!oldestLogDate.HasValue || chatLogInfo.LastWriteTime < oldestLogDate.Value)
                {
                    oldestLogDate = chatLogInfo.LastWriteTime;
                }
            }
            
            // Vérifier wakfu.log
            if (!string.IsNullOrEmpty(_kikimeterLogPath) && File.Exists(_kikimeterLogPath))
            {
                var kikimeterLogInfo = new FileInfo(_kikimeterLogPath);
                if (!oldestLogDate.HasValue || kikimeterLogInfo.LastWriteTime < oldestLogDate.Value)
                {
                    oldestLogDate = kikimeterLogInfo.LastWriteTime;
                }
            }
            
            // Si aucun log trouvé, ne pas tronquer
            if (!oldestLogDate.HasValue)
            {
                Logger.Debug("LootCharacterDetector", "Aucun log trouvé, pas de troncature");
                return false;
            }
            
            // Si les logs sont plus anciens que la dernière exécution, c'est une réinstallation
            // On ajoute une marge de 1 heure pour gérer les cas limites (décalage horaire, etc.)
            bool shouldTruncate = oldestLogDate.Value < lastAppRun.AddHours(-1);
            
            if (shouldTruncate)
            {
                Logger.Info("LootCharacterDetector", $"Logs détectés comme anciens: dernière modif={oldestLogDate.Value:yyyy-MM-dd HH:mm:ss}, dernière exécution={lastAppRun:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                Logger.Debug("LootCharacterDetector", $"Logs détectés comme récents: dernière modif={oldestLogDate.Value:yyyy-MM-dd HH:mm:ss}, dernière exécution={lastAppRun:yyyy-MM-dd HH:mm:ss}");
            }
            
            return shouldTruncate;
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Erreur lors de la vérification des logs: {ex.Message}");
            // En cas d'erreur, ne pas tronquer pour éviter de perdre des données
            return false;
        }
    }
    
    /// <summary>
    /// Marque la date de cette exécution dans la configuration
    /// </summary>
    private void MarkAppRun()
    {
        try
        {
            var config = LoadConfig();
            config.LastAppRun = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveConfig(config);
            Logger.Debug("LootCharacterDetector", $"Date d'exécution marquée: {config.LastAppRun}");
        }
        catch (Exception ex)
        {
            Logger.Warning("LootCharacterDetector", $"Erreur lors du marquage de l'exécution: {ex.Message}");
        }
    }

    public void ResetCharacterStorage(bool rehydrateAfterReset = false, bool suppressServerEvents = false)
    {
        Logger.Info("LootCharacterDetector", "Réinitialisation demandée des personnages détectés");

        // Utiliser la méthode avec troncature des logs pour éviter de recharger les anciens personnages
        ResetStoredCharactersWithLogTruncation();

        bool previousSuppression = _suppressServerNotifications;
        if (suppressServerEvents)
        {
            _suppressServerNotifications = true;
        }

        bool repopulated = false;

        try
        {
            if (rehydrateAfterReset)
            {
                InitializeLogPositions(initialLoad: true);
                repopulated = RehydrateCharactersAfterReset();
            }
            else
            {
                InitializeLogPositions(initialLoad: false);
                ReplayServerContextFromTail();
            }
        }
        finally
        {
            if (suppressServerEvents)
            {
                _suppressServerNotifications = previousSuppression;
            }
        }

        try
        {
            var config = LoadConfig();
            if (!repopulated)
            {
                CharactersChanged?.Invoke(this, config.Characters.Keys.ToList());
            }

            if (!string.IsNullOrWhiteSpace(config.MainCharacter))
            {
                MainCharacterDetected?.Invoke(this, config.MainCharacter);
            }
            else
            {
                MainCharacterDetected?.Invoke(this, string.Empty);
            }
        }
        catch (Exception notifyEx)
        {
            Logger.Warning("LootCharacterDetector", $"Impossible de notifier après reset: {notifyEx.Message}");
        }
    }
    
    public string ConfigFilePath => _configFilePath;
    
    public void SetCharacterVisibility(string characterName, bool isVisible)
    {
        var config = LoadConfig();
        
        // Ne jamais permettre de désactiver la visibilité du personnage principal
        if (string.Equals(characterName, config.MainCharacter, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info("LootCharacterDetector", $"Tentative de désactiver la visibilité du personnage principal {characterName} - ignorée");
            // Forcer la visibilité à true
            if (config.Characters.ContainsKey(characterName))
            {
                config.Characters[characterName] = true;
                SaveConfig(config);
            }
            return;
        }
        
        if (config.Characters.ContainsKey(characterName))
        {
            config.Characters[characterName] = isVisible;
            SaveConfig(config);
        }
    }

    public void RegisterCombatPlayers(IEnumerable<string> playerNames)
    {
        if (playerNames == null)
        {
            return;
        }

        bool updated = false;
        var now = DateTime.Now;
        
        // Créer un set des joueurs actuels dans le combat
        var currentCombatPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var playerName in playerNames)
        {
            var normalized = NormalizeCharacterName(playerName);
            if (string.IsNullOrEmpty(normalized))
            {
                continue;
            }

            if (string.Equals(normalized, "Vous", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _recentCharacters[normalized] = now;
            _playersInCombat.Add(normalized); // Marquer comme étant dans le combat
            currentCombatPlayers.Add(normalized);
            updated = true;
        }
        
        // Retirer les joueurs qui ne sont plus dans le combat ET qui ne sont pas dans le groupe
        var playersToRemove = _playersInCombat
            .Where(p => !currentCombatPlayers.Contains(p) && !_playersInGroup.Contains(p))
            .ToList();
            
        foreach (var playerToRemove in playersToRemove)
        {
            _playersInCombat.Remove(playerToRemove);
            _recentCharacters.Remove(playerToRemove);
            Logger.Info("LootCharacterDetector", $"Joueur retiré (a quitté le combat et n'est pas dans le groupe): {playerToRemove}");
        }

        if (updated || playersToRemove.Count > 0)
        {
            Logger.Info("LootCharacterDetector", $"RegisterCombatPlayers: {currentCombatPlayers.Count} dans le combat, {_playersInGroup.Count} dans le groupe");
            UpdateRecentCharacters();
        }
        else
        {
            Logger.Debug("LootCharacterDetector", $"RegisterCombatPlayers appelé mais aucun personnage valide trouvé");
        }
    }

    private void ResetStoredCharacters()
    {
        _recentCharacters.Clear();
        _playersInGroup.Clear();
        _playersInCombat.Clear();
        try
        {
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var emptyConfig = new LootCharacterConfig
            {
                LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var json = JsonConvert.SerializeObject(emptyConfig, Formatting.Indented);
            File.WriteAllText(_configFilePath, json, Encoding.UTF8);
            Logger.Info("LootCharacterDetector", $"Configuration réinitialisée ({_configFilePath})");
            
            // Forcer la mise à jour de la liste pour notifier les UI
            var characterList = emptyConfig.Characters.Keys.ToList();
            CharactersChanged?.Invoke(this, characterList);
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Impossible de réinitialiser loot_characters.json: {ex.Message}");
        }
    }
    
    private void ResetStoredCharactersWithLogTruncation()
    {
        _recentCharacters.Clear();
        _playersInGroup.Clear();
        _playersInCombat.Clear();
        
        // Sauvegarder LastAppRun avant de réinitialiser la config
        string? savedLastAppRun = null;
        try
        {
            if (File.Exists(_configFilePath))
            {
                var existingConfig = LoadConfig();
                savedLastAppRun = existingConfig.LastAppRun;
            }
        }
        catch
        {
            // Ignorer les erreurs de chargement
        }
        
        // Tronquer les fichiers de logs pour éviter de recharger les anciens personnages
        TryTruncateLogFile(_logFilePath, "wakfu_chat.log");
        TryTruncateLogFile(_kikimeterLogPath, "wakfu.log");
        
        // Réinitialiser les positions de lecture
        _lastPosition = 0;
        _lastKikimeterPosition = 0;
        
        try
        {
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var emptyConfig = new LootCharacterConfig
            {
                LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                LastAppRun = savedLastAppRun // Préserver LastAppRun si disponible
            };

            var json = JsonConvert.SerializeObject(emptyConfig, Formatting.Indented);
            File.WriteAllText(_configFilePath, json, Encoding.UTF8);
            Logger.Info("LootCharacterDetector", $"Configuration réinitialisée avec troncature des logs ({_configFilePath})");
            
            // Forcer la mise à jour de la liste pour notifier les UI
            var characterList = emptyConfig.Characters.Keys.ToList();
            CharactersChanged?.Invoke(this, characterList);
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Impossible de réinitialiser loot_characters.json: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Réinitialise complètement les personnages (fichier + mémoire + logs) sans recharger depuis les logs
    /// </summary>
    public void FullReset()
    {
        Logger.Info("LootCharacterDetector", "Réinitialisation complète demandée (avec troncature des logs)");
        ResetStoredCharactersWithLogTruncation();
        // Ne pas appeler RehydrateCharactersAfterReset() pour une réinitialisation complète
        Logger.Info("LootCharacterDetector", "Réinitialisation complète terminée");
    }

    private void TryTruncateLogFile(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            stream.SetLength(0);
            Logger.Info("LootCharacterDetector", $"Fichier {description} tronqué au démarrage");
        }
        catch (Exception ex)
        {
            Logger.Warning("LootCharacterDetector", $"Impossible de tronquer {description}: {ex.Message}");
        }
    }
    
    public void SetMainCharacter(string characterName)
    {
        var config = LoadConfig();
        characterName = NormalizeCharacterName(characterName);
        
        // S'assurer que le personnage existe dans la liste des personnages
        if (!config.Characters.ContainsKey(characterName))
        {
            config.Characters[characterName] = true;
        }
        else
        {
            // FORCER la visibilité à true pour le personnage principal
            config.Characters[characterName] = true;
        }
        
            config.MainCharacter = characterName;
        config.LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // S'assurer que le personnage principal est dans MyCharacters
        if (config.MyCharacters == null)
        {
            config.MyCharacters = new List<string>();
        }
        
        // Retirer le personnage de MyCharacters s'il y est déjà
        config.MyCharacters.RemoveAll(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase));
        // L'ajouter en premier
                config.MyCharacters.Insert(0, characterName);
                // Limiter à 3 personnages
                if (config.MyCharacters.Count > 3)
                {
                    config.MyCharacters = config.MyCharacters.Take(3).ToList();
            }
            
            SaveConfig(config);
        Logger.Info("LootCharacterDetector", $"Personnage principal défini et sauvegardé: {characterName}");
            MainCharacterDetected?.Invoke(this, characterName);
    }
    
    public void AddToMyCharacters(string characterName)
    {
        var config = LoadConfig();
        if (config.Characters.ContainsKey(characterName) && 
            !config.MyCharacters.Contains(characterName, StringComparer.OrdinalIgnoreCase))
        {
            config.MyCharacters.Add(characterName);
            // Limiter à 3 personnages (principal + 2 autres)
            if (config.MyCharacters.Count > 3)
            {
                config.MyCharacters = config.MyCharacters.Take(3).ToList();
            }
            SaveConfig(config);
        }
    }
    
    public void RemoveFromMyCharacters(string characterName)
    {
        var config = LoadConfig();
        // Ne pas permettre de retirer le personnage principal
        if (string.Equals(characterName, config.MainCharacter, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        
        config.MyCharacters.RemoveAll(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase));
        SaveConfig(config);
    }
    
    /// <summary>
    /// Retire complètement un personnage de la liste (sauf le personnage principal)
    /// </summary>
    public void RemoveCharacter(string characterName)
    {
        var config = LoadConfig();
        
        // Ne pas permettre de retirer le personnage principal
        if (string.Equals(characterName, config.MainCharacter, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warning("LootCharacterDetector", $"Impossible de retirer le personnage principal: {characterName}");
            return;
        }
        
        // Retirer du dictionnaire de personnages
        if (config.Characters.ContainsKey(characterName))
        {
            config.Characters.Remove(characterName);
        }
        
        // Retirer de "mes personnages" si présent
        config.MyCharacters.RemoveAll(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase));

        // Retirer de la liste des personnages manuels
        if (config.ManualCharacters != null)
        {
            config.ManualCharacters.RemoveAll(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase));
        }
        
        // Retirer aussi de _recentCharacters
        if (_recentCharacters.ContainsKey(characterName))
        {
            _recentCharacters.Remove(characterName);
        }
        
        SaveConfig(config);
        
        // Notifier le changement
        var characterList = config.Characters.Keys.ToList();
        CharactersChanged?.Invoke(this, characterList);
        
        Logger.Info("LootCharacterDetector", $"Personnage retiré de la liste: {characterName}");
    }
    
    /// <summary>
    /// Force un scan manuel des nouveaux personnages (pour mise à jour périodique)
    /// </summary>
    public void ManualScan(bool suppressServerNotifications = false)
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            bool previousSuppression = _suppressServerNotifications;
            if (suppressServerNotifications)
            {
                _suppressServerNotifications = true;
            }

            try
            {
                ScanNewCharacters();
            }
            catch (Exception ex)
            {
                Logger.Error("LootCharacterDetector", $"Erreur lors du scan manuel: {ex.Message}");
            }
            finally
            {
                if (suppressServerNotifications)
                {
                    _suppressServerNotifications = previousSuppression;
                }
            }
        });
    }
    
    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _kikimeterFileWatcher?.Dispose();
    }

    private void InitializeLogPositions(bool initialLoad)
    {
        try
        {
            if (initialLoad)
            {
                PerformInitialScan();
            }
            else
            {
                _lastPosition = File.Exists(_logFilePath) ? new FileInfo(_logFilePath).Length : 0;
                _lastKikimeterPosition = (!string.IsNullOrEmpty(_kikimeterLogPath) && File.Exists(_kikimeterLogPath))
                    ? new FileInfo(_kikimeterLogPath).Length
                    : 0;
                Logger.Info("LootCharacterDetector", $"Positions repositionnées après reset (chat={_lastPosition}, combat={_lastKikimeterPosition}).");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("LootCharacterDetector", $"Erreur lors de l'initialisation des pointeurs de logs: {ex.Message}");
            _lastPosition = 0;
            _lastKikimeterPosition = 0;
        }
    }

    private bool RehydrateCharactersAfterReset()
    {
        bool changed = false;

        // D'abord, recharger les personnages depuis la config existante pour les rendre visibles
        var config = LoadConfig();
        foreach (var character in config.Characters.Keys)
        {
            if (!_recentCharacters.ContainsKey(character))
            {
                _recentCharacters[character] = DateTime.Now;
                changed = true;
                Logger.Debug("LootCharacterDetector", $"Personnage rechargé depuis la config: {character}");
            }
        }

        // Ensuite, scanner les logs pour détecter de nouveaux personnages
        if (!string.IsNullOrEmpty(_kikimeterLogPath) && File.Exists(_kikimeterLogPath))
        {
            changed |= ScanRecentSegment(_kikimeterLogPath, true);
        }

        if (File.Exists(_logFilePath))
        {
            changed |= ScanRecentSegment(_logFilePath, false);
        }

        if (changed)
        {
            Logger.Info("LootCharacterDetector", "Personnages récents reprovisionnés après reset.");
            UpdateRecentCharacters();
        }
        else
        {
            // Même si aucun nouveau personnage n'a été détecté, mettre à jour pour notifier les UI
            UpdateRecentCharacters();
            Logger.Debug("LootCharacterDetector", "Aucun nouveau personnage détecté après reset, mais personnages existants rechargés.");
        }

        return changed;
    }

    private bool ScanRecentSegment(string path, bool isKikimeterLog)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            long length = fileInfo.Length;
            long startPosition = Math.Max(0, length - RECENT_LOG_TAIL_BYTES);

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(startPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);

            if (startPosition > 0)
            {
                reader.ReadLine(); // ignorer ligne partielle
            }

            string? line;
            bool changed = false;

            while ((line = reader.ReadLine()) != null)
            {
                if (isKikimeterLog)
                {
                    if (line.Contains("join the fight at") && line.Contains("isControlledByAI=false"))
                    {
                        var match = PlayerJoinRegex.Match(line);
                        if (match.Success)
                        {
                            string playerName = NormalizeCharacterName(match.Groups[1].Value);
                            if (!string.IsNullOrEmpty(playerName) && !string.Equals(playerName, "Vous", StringComparison.OrdinalIgnoreCase))
                            {
                                _recentCharacters[playerName] = DateTime.Now;
                                _playersInCombat.Add(playerName); // Marquer comme étant dans le combat
                                changed = true;
                                Logger.Debug("LootCharacterDetector", $"Rehydratation (wakfu.log): {playerName}");
                            }
                        }
                    }
                }
                else
                {
                    if (DetectCharacterInChatLogLine(line, DateTime.Now))
                    {
                        changed = true;
                        Logger.Debug("LootCharacterDetector", "Rehydratation (wakfu_chat.log) - nouveau personnage détecté.");
                    }
                }
            }

            if (isKikimeterLog)
            {
                _lastKikimeterPosition = length;
            }
            else
            {
                _lastPosition = length;
            }

            return changed;
        }
        catch (Exception ex)
        {
            Logger.Warning("LootCharacterDetector", $"Impossible de scanner le segment récent ({path}): {ex.Message}");
            return false;
        }
    }

    private void HandleServerChangeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var match = ServerConnectionRegex.Match(line);
        if (match.Success)
        {
            var rawHost = match.Groups["host"].Value;
            var serverName = ExtractServerName(rawHost);

            if (string.IsNullOrEmpty(serverName))
            {
                return;
            }

            if (string.Equals(serverName, "dispatcher", StringComparison.OrdinalIgnoreCase))
            {
                lock (_serverLock)
                {
                    if (!string.IsNullOrEmpty(_currentServer))
                    {
                        Logger.Debug("LootCharacterDetector", "Connexion au dispatcher détectée, serveur courant réinitialisé.");
                    }
                    _currentServer = string.Empty;
                }

                return;
            }

            bool shouldRaise = false;
            bool isNewConnection = false;

            lock (_serverLock)
            {
                if (!string.Equals(_currentServer, serverName, StringComparison.OrdinalIgnoreCase))
                {
                    // Si on passe d'un serveur vide à un nouveau serveur, c'est une nouvelle connexion
                    isNewConnection = string.IsNullOrEmpty(_currentServer);
                    _currentServer = serverName;
                    shouldRaise = true;
                }
            }

            if (shouldRaise && !_suppressServerNotifications)
            {
                Logger.Info("LootCharacterDetector", $"Changement de serveur détecté: {serverName}");
                
                // Si c'est une nouvelle connexion (après déconnexion), réinitialiser et relecture des logs
                if (isNewConnection)
                {
                    Logger.Info("LootCharacterDetector", "Nouvelle connexion détectée, réinitialisation des personnages et relecture des logs");
                    ResetCharactersForNewConnection();
                }
                
                ServerChanged?.Invoke(this, new ServerChangeDetectedEventArgs(serverName, line, false));
            }

            return;
        }

        if (line.Contains("Reason", StringComparison.OrdinalIgnoreCase) &&
            (line.Contains("{LogOff}", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("{Dispatch}", StringComparison.OrdinalIgnoreCase)))
        {
            bool hadServer;
            lock (_serverLock)
            {
                hadServer = !string.IsNullOrEmpty(_currentServer);
                _currentServer = string.Empty;
            }

            if (hadServer)
            {
                Logger.Info("LootCharacterDetector", "Déconnexion détectée, réinitialisation des personnages");
                // Réinitialiser les personnages à la déconnexion (fermeture du jeu)
                ResetCharactersOnDisconnect();
            }
        }
    }
    
    /// <summary>
    /// Réinitialise les personnages à la déconnexion (fermeture du jeu)
    /// On conserve TOUS les personnages de la config, on vide seulement les listes en mémoire
    /// </summary>
    private void ResetCharactersOnDisconnect()
    {
        try
        {
            // Vider seulement les listes en mémoire (groupe et combat)
            // Mais CONSERVER tous les personnages dans la config pour qu'ils restent visibles dans les paramètres
            _playersInGroup.Clear();
            _playersInCombat.Clear();
            
            // Ne PAS vider _recentCharacters car ils sont utilisés pour l'affichage
            // Ne PAS modifier la config, on veut garder tous les personnages détectés
            
            Logger.Info("LootCharacterDetector", "Listes de groupe et combat réinitialisées à la déconnexion (personnages conservés dans la config)");
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Erreur lors de la réinitialisation à la déconnexion: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Réinitialise et relit les logs à la connexion au serveur
    /// </summary>
    private void ResetCharactersForNewConnection()
    {
        try
        {
            // Réinitialiser seulement les listes de groupe et combat
            // CONSERVER _recentCharacters pour garder les personnages visibles
            _playersInGroup.Clear();
            _playersInCombat.Clear();
            
            // Réinitialiser les positions de lecture pour relire depuis le début
            _lastPosition = 0;
            _lastKikimeterPosition = 0;
            
            // Relecture des logs pour détecter les joueurs actuels
            Logger.Info("LootCharacterDetector", "Relecture des logs pour détecter les joueurs actuels");
            InitializeLogPositions(initialLoad: true);
            RehydrateCharactersAfterReset();
            
            Logger.Info("LootCharacterDetector", "Relecture des logs terminée");
        }
        catch (Exception ex)
        {
            Logger.Error("LootCharacterDetector", $"Erreur lors de la réinitialisation à la connexion: {ex.Message}");
        }
    }

    private static string ExtractServerName(string rawHost)
    {
        if (string.IsNullOrWhiteSpace(rawHost))
        {
            return string.Empty;
        }

        var host = rawHost.Trim();

        if (host.StartsWith("wakfu-", StringComparison.OrdinalIgnoreCase))
        {
            host = host.Substring("wakfu-".Length);
        }

        var separators = new[] { '.', ':' };
        var separatorIndex = host.IndexOfAny(separators);
        if (separatorIndex >= 0)
        {
            host = host.Substring(0, separatorIndex);
        }

        return host.Trim();
    }
}

public sealed class ServerChangeDetectedEventArgs : EventArgs
{
    public ServerChangeDetectedEventArgs(string serverName, string sourceLine, bool isDisconnect)
    {
        ServerName = serverName;
        SourceLine = sourceLine;
        IsDisconnect = isDisconnect;
    }

    public string ServerName { get; }
    public string SourceLine { get; }
    public bool IsDisconnect { get; }
}


