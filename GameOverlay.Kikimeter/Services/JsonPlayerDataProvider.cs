using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Models;
using Newtonsoft.Json;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Implémentation de IPlayerDataProvider basée sur le polling JSON
/// Lit périodiquement un fichier JSON pour obtenir les données des joueurs
/// </summary>
public class JsonPlayerDataProvider : IPlayerDataProvider, IDisposable
{
    private const string LogCategory = "JsonPlayerDataProvider";
    private const string DefaultJsonFileName = "player_data.json";
    private const int DefaultPollingIntervalMs = 1000; // 1 seconde par défaut

    private readonly string _jsonFilePath;
    private readonly int _pollingIntervalMs;
    private readonly System.Threading.Timer _pollingTimer;
    private readonly object _dataLock = new object();
    
    private PlayerDataJson _cachedData = new PlayerDataJson();
    private DateTime _lastReadTime = DateTime.MinValue;
    private bool _disposed = false;

    /// <summary>
    /// Événement déclenché lorsque les données sont mises à jour
    /// </summary>
    public event EventHandler? DataUpdated;

    public JsonPlayerDataProvider(string? jsonFilePath = null, int pollingIntervalMs = DefaultPollingIntervalMs)
    {
        // Déterminer le chemin du fichier JSON
        if (string.IsNullOrEmpty(jsonFilePath))
        {
            // Utiliser le dossier AppData par défaut
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant",
                "Kikimeter"
            );
            
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }
            
            _jsonFilePath = Path.Combine(appDataDir, DefaultJsonFileName);
        }
        else
        {
            _jsonFilePath = jsonFilePath;
            var directory = Path.GetDirectoryName(_jsonFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        _pollingIntervalMs = pollingIntervalMs;

        // Initialiser le fichier JSON s'il n'existe pas (ne bloque jamais le démarrage)
        PlayerDataJsonInitializer.EnsurePlayerDataJsonExists();

        // Charger les données initiales
        LoadDataFromJson();

        // Démarrer le polling périodique
        _pollingTimer = new System.Threading.Timer(PollJsonFile, null, 0, _pollingIntervalMs);

        Logger.Info(LogCategory, $"JsonPlayerDataProvider initialisé avec le fichier: {_jsonFilePath} (polling: {_pollingIntervalMs}ms)");
    }

    public Dictionary<string, PlayerData> GetCurrentPlayers()
    {
        lock (_dataLock)
        {
            var result = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);

            foreach (var playerItem in _cachedData.Players)
            {
                var playerData = new PlayerData
                {
                    Id = playerItem.Id,
                    Name = playerItem.Name,
                    IsMainCharacter = playerItem.IsMainCharacter,
                    IsInGroup = playerItem.IsInGroup,
                    LastSeenInCombat = playerItem.LastSeenInCombat,
                    IsActive = playerItem.IsActive
                };

                // Permettre la recherche par ID et par nom
                if (!string.IsNullOrEmpty(playerData.Id))
                {
                    result[playerData.Id] = playerData;
                }
                if (!string.IsNullOrEmpty(playerData.Name))
                {
                    result[playerData.Name] = playerData;
                }
            }

            return result;
        }
    }

    public HashSet<string> GetPlayersInCombat()
    {
        lock (_dataLock)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!_cachedData.CombatActive)
            {
                return result;
            }

            // Retourner tous les joueurs actifs comme étant en combat
            foreach (var playerItem in _cachedData.Players)
            {
                if (playerItem.IsActive)
                {
                    if (!string.IsNullOrEmpty(playerItem.Id))
                    {
                        result.Add(playerItem.Id);
                    }
                    if (!string.IsNullOrEmpty(playerItem.Name))
                    {
                        result.Add(playerItem.Name);
                    }
                }
            }

            return result;
        }
    }

    public bool IsCombatActive
    {
        get
        {
            lock (_dataLock)
            {
                return _cachedData.CombatActive;
            }
        }
    }

    /// <summary>
    /// Obtient le nom du serveur actuel depuis le JSON
    /// </summary>
    public string? GetCurrentServerName()
    {
        lock (_dataLock)
        {
            return _cachedData.ServerName;
        }
    }

    /// <summary>
    /// Méthode de polling appelée périodiquement par le Timer
    /// </summary>
    private void PollJsonFile(object? state)
    {
        if (_disposed)
            return;

        try
        {
            LoadDataFromJson();
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors du polling JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Charge les données depuis le fichier JSON
    /// </summary>
    private void LoadDataFromJson()
    {
        if (!File.Exists(_jsonFilePath))
        {
            // Fichier n'existe pas encore, créer un fichier vide
            if (_cachedData.Players.Count == 0 && !_cachedData.CombatActive)
            {
                // Pas besoin de notifier si les données sont déjà vides
                return;
            }

            // Fichier supprimé, réinitialiser les données
            lock (_dataLock)
            {
                _cachedData = new PlayerDataJson();
            }

            OnDataUpdated();
            return;
        }

        try
        {
            var fileInfo = new FileInfo(_jsonFilePath);
            var lastWriteTime = fileInfo.LastWriteTime;

            // Éviter de relire si le fichier n'a pas changé
            if (lastWriteTime <= _lastReadTime)
            {
                return;
            }

            var jsonContent = File.ReadAllText(_jsonFilePath);
            
            // Gérer les fichiers vides ou contenant uniquement des espaces
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                Logger.Debug(LogCategory, "Le fichier JSON est vide, utilisation des données par défaut");
                lock (_dataLock)
                {
                    _cachedData = new PlayerDataJson();
                }
                OnDataUpdated();
                return;
            }

            PlayerDataJson? newData;
            try
            {
                newData = JsonConvert.DeserializeObject<PlayerDataJson>(jsonContent);
            }
            catch (JsonException ex)
            {
                Logger.Warning(LogCategory, $"Le fichier JSON est invalide ({ex.Message}), utilisation des données par défaut");
                lock (_dataLock)
                {
                    _cachedData = new PlayerDataJson();
                }
                OnDataUpdated();
                return;
            }

            if (newData == null)
            {
                Logger.Warning(LogCategory, "Le fichier JSON est null après désérialisation, utilisation des données par défaut");
                lock (_dataLock)
                {
                    _cachedData = new PlayerDataJson();
                }
                OnDataUpdated();
                return;
            }

            bool dataChanged = false;

            lock (_dataLock)
            {
                // Comparer les données pour détecter les changements
                if (HasDataChanged(_cachedData, newData))
                {
                    _cachedData = newData;
                    dataChanged = true;
                }
            }

            _lastReadTime = lastWriteTime;

            if (dataChanged)
            {
                Logger.Debug(LogCategory, $"Données JSON mises à jour: {newData.Players.Count} joueurs, combat actif: {newData.CombatActive}");
                OnDataUpdated();
            }
        }
        catch (JsonException ex)
        {
            Logger.Error(LogCategory, $"Erreur de parsing JSON: {ex.Message}");
        }
        catch (IOException ex)
        {
            // Fichier peut être en cours d'écriture, ignorer silencieusement
            Logger.Debug(LogCategory, $"Fichier JSON verrouillé (en cours d'écriture): {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la lecture du fichier JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Compare deux ensembles de données pour détecter les changements
    /// </summary>
    private bool HasDataChanged(PlayerDataJson oldData, PlayerDataJson newData)
    {
        if (oldData.CombatActive != newData.CombatActive)
            return true;

        if (oldData.Players.Count != newData.Players.Count)
            return true;

        if (!string.Equals(oldData.ServerName, newData.ServerName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Comparer les joueurs
        var oldPlayersDict = oldData.Players.ToDictionary(
            p => p.Id + "|" + p.Name,
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var newPlayer in newData.Players)
        {
            var key = newPlayer.Id + "|" + newPlayer.Name;
            if (!oldPlayersDict.TryGetValue(key, out var oldPlayer))
            {
                return true; // Nouveau joueur
            }

            // Comparer les propriétés importantes
            if (oldPlayer.IsActive != newPlayer.IsActive ||
                oldPlayer.IsInGroup != newPlayer.IsInGroup ||
                oldPlayer.IsMainCharacter != newPlayer.IsMainCharacter ||
                Math.Abs((oldPlayer.LastSeenInCombat - newPlayer.LastSeenInCombat).TotalSeconds) > 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Déclenche l'événement DataUpdated
    /// </summary>
    protected virtual void OnDataUpdated()
    {
        DataUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pollingTimer?.Dispose();
        Logger.Info(LogCategory, "JsonPlayerDataProvider disposé");
    }
}
