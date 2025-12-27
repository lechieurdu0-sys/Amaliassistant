using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GameOverlay.Kikimeter.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service pour surveiller et parser les items ramassés depuis wakfu_chat.log
/// Détecte les loots pour "Vous" et les autres personnages
/// </summary>
public class LootTracker : IDisposable
{
    private readonly string _logFilePath;
    private readonly Dictionary<string, LootItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lockObject = new object();
    private long _lastPosition = 0;
    private readonly FileSystemWatcher? _fileWatcher;
    private string? _mainCharacterName = null; // Nom du personnage principal (remplace "Vous")
    
    // Regex pour "Vous avez ramassé Xx NomItem ."
    private static readonly Regex PlayerLootRegex = new Regex(
        @"Vous avez ramassé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "[Pseudo] a ramassé Xx NomItem ."
    private static readonly Regex OtherPlayerLootRegex = new Regex(
        @"^([\p{L}0-9\-_'’ ]+?)\s+a ramassé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    public event EventHandler<LootItem>? LootItemAdded;
    public event EventHandler<LootItem>? LootItemUpdated;
    
    public LootTracker(string logFilePath)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        
        // Initialiser la position à la fin du fichier si le fichier existe (ne pas lire l'historique)
        if (File.Exists(_logFilePath))
        {
            try
            {
                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Length > 0)
                {
                    using var reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    reader.BaseStream.Seek(0, SeekOrigin.End);
                    _lastPosition = reader.BaseStream.Position;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("LootTracker", $"Erreur lors de l'initialisation: {ex.Message}");
            }
        }
        else
        {
            Logger.Info("LootTracker", $"Fichier de log n'existe pas encore: {_logFilePath} - La surveillance attendra sa création");
        }
        
        // Créer le FileSystemWatcher même si le fichier n'existe pas encore
        // Il détectera la création du fichier via l'événement Created
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            var fileName = Path.GetFileName(_logFilePath);
            
            if (directory != null)
            {
                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName
                };
                
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Created += OnFileChanged; // Détecter la création du fichier
                _fileWatcher.EnableRaisingEvents = true;
                Logger.Info("LootTracker", $"FileSystemWatcher initialisé pour {_logFilePath} (fichier peut ne pas exister encore)");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("LootTracker", $"Erreur lors de la création du FileSystemWatcher: {ex.Message}");
        }
        
        Logger.Info("LootTracker", $"LootTracker initialisé pour: {_logFilePath}");
    }
    
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Si le fichier vient d'être créé, réinitialiser la position
        if (e.ChangeType == WatcherChangeTypes.Created)
        {
            _lastPosition = 0;
            Logger.Info("LootTracker", $"Fichier de log créé détecté: {_logFilePath}");
        }
        
        // Délai pour éviter les lectures multiples lors d'une écriture en cours
        System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => ReadNewLines());
    }
    
    private void ReadNewLines()
    {
        if (!File.Exists(_logFilePath))
        {
            // Si le fichier n'existe pas encore, réinitialiser la position pour qu'il soit lu depuis le début quand il sera créé
            _lastPosition = 0;
            return;
        }
        
        lock (_lockObject)
        {
            try
            {
                using var reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                if (_lastPosition > reader.BaseStream.Length)
                {
                    _lastPosition = 0;
                    Logger.Info("LootTracker", "Fichier de log réinitialisé (taille réduite). Reprise de la lecture depuis le début.");
                }

                reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
                
                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    return; // Pas de nouvelles lignes
                
                string? line;
                while (true)
                {
                    line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    ProcessLine(line);
                }
                
                _lastPosition = reader.BaseStream.Position;
            }
            catch (Exception ex)
            {
                Logger.Error("LootTracker", $"Erreur lors de la lecture: {ex.Message}");
            }
        }
    }
    
    private void ProcessLine(string line)
    {
        // Nettoyer la ligne (enlever les préfixes timestamp et [Information (jeu)])
        string cleanLine = line;
        const string infoMarker = "[Information (jeu)] ";
        int infoIndex = line.IndexOf(infoMarker, StringComparison.OrdinalIgnoreCase);
        if (infoIndex >= 0)
        {
            cleanLine = line[(infoIndex + infoMarker.Length)..].Trim();
        }
        else
        {
            int fallbackIndex = line.LastIndexOf("] ", StringComparison.Ordinal);
            if (fallbackIndex >= 0)
            {
                cleanLine = line[(fallbackIndex + 2)..].Trim();
            }
        }
        
        // Essayer d'abord le pattern pour "Vous"
        var match = PlayerLootRegex.Match(cleanLine);
        if (match.Success)
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            // Utiliser le nom du personnage principal si défini, sinon "Vous"
            string characterName = _mainCharacterName ?? "Vous";
            ProcessLoot(characterName, itemName, quantity, line);
            return;
        }
        else if (cleanLine.Contains("ramass", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Debug("LootTracker", $"Ligne loot non reconnue: {cleanLine}");
        }
        
        // Essayer ensuite le pattern pour les autres personnages
        match = OtherPlayerLootRegex.Match(cleanLine);
        if (match.Success)
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            ProcessLoot(characterName, itemName, quantity, line);
            return;
        }
    }
    
    private void ProcessLoot(string characterName, string itemName, int quantity, string originalLine)
    {
        // Utiliser le nom tel quel (déjà remplacé si nécessaire)
        string actualCharacterName = characterName;
        
        // Créer une clé unique : CharacterName + ItemName
        string key = $"{actualCharacterName}|{itemName}";
        
        lock (_lockObject)
        {
            if (_items.TryGetValue(key, out LootItem? existingItem))
            {
                // Item existant : incrémenter la quantité
                Logger.Debug("LootTracker", $"Item existant {actualCharacterName}: {itemName} - quantité avant: {existingItem.Quantity}, ajout: {quantity}");
                existingItem.AddQuantity(quantity);
                LootItemUpdated?.Invoke(this, existingItem);
            }
            else
            {
                // Nouvel item : créer et ajouter
                LootItem newItem = new LootItem(actualCharacterName, itemName, quantity);
                _items[key] = newItem;
                Logger.Debug("LootTracker", $"NOUVEL ITEM {actualCharacterName}: {itemName} x{quantity}");
                LootItemAdded?.Invoke(this, newItem);
            }
        }
    }
    
    /// <summary>
    /// Définit le nom du personnage principal (remplace "Vous")
    /// </summary>
    public void SetMainCharacter(string mainCharacterName)
    {
        lock (_lockObject)
        {
            _mainCharacterName = mainCharacterName;
            
            // Mettre à jour tous les items qui ont "Vous" comme nom de personnage
            var itemsToUpdate = _items.Where(kvp => string.Equals(kvp.Value.CharacterName, "Vous", StringComparison.OrdinalIgnoreCase)).ToList();
            
            foreach (var kvp in itemsToUpdate)
            {
                // Créer une nouvelle clé avec le nouveau nom
                string newKey = $"{mainCharacterName}|{kvp.Value.ItemName}";
                
                // Vérifier si un item avec ce nom existe déjà
                if (_items.TryGetValue(newKey, out LootItem? existingItem))
                {
                    // Fusionner les quantités
                    existingItem.AddQuantity(kvp.Value.Quantity);
                    _items.Remove(kvp.Key);
                }
                else
                {
                    // Mettre à jour le nom du personnage dans l'item
                    // Note: On ne peut pas modifier CharacterName car c'est une propriété en lecture seule
                    // On doit créer un nouvel item et supprimer l'ancien
                    var oldItem = kvp.Value;
                    var newItem = new LootItem(mainCharacterName, oldItem.ItemName, oldItem.Quantity);
                    _items[newKey] = newItem;
                    _items.Remove(kvp.Key);
                }
            }
            
            Logger.Info("LootTracker", $"Personnage principal défini: {mainCharacterName}");
        }
    }
    
    /// <summary>
    /// Obtient le nom du personnage principal actuel
    /// </summary>
    public string? GetMainCharacter()
    {
        lock (_lockObject)
        {
            return _mainCharacterName;
        }
    }
    
    public IReadOnlyDictionary<string, LootItem> GetItems()
    {
        lock (_lockObject)
        {
            return new Dictionary<string, LootItem>(_items, StringComparer.OrdinalIgnoreCase);
        }
    }
    
    public void Clear()
    {
        lock (_lockObject)
        {
            _items.Clear();
        }
    }
    
    /// <summary>
    /// Force la lecture des nouvelles lignes (comme ManualRead du Kikimeter)
    /// Utile pour l'actualisation en temps réel via un timer
    /// </summary>
    public void ManualRead()
    {
        System.Threading.Tasks.Task.Run(() => ReadNewLines());
    }
    
    public void RemoveItem(string characterName, string itemName)
    {
        string key = $"{characterName}|{itemName}";
        lock (_lockObject)
        {
            _items.Remove(key);
        }
    }
    
    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}

