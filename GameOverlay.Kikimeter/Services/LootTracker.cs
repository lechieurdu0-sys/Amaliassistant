using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    
    // Regex pour "Vous avez retiré Xx NomItem ."
    private static readonly Regex PlayerRemovedRegex = new Regex(
        @"Vous avez retiré\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "[Pseudo] a retiré Xx NomItem ."
    private static readonly Regex OtherPlayerRemovedRegex = new Regex(
        @"^([\p{L}0-9\-_'’ ]+?)\s+a retiré\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "Vous avez perdu Xx NomItem ." (utilisé pour destruction/bris/dépôt)
    private static readonly Regex PlayerLostRegex = new Regex(
        @"Vous avez perdu\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "[Pseudo] a perdu Xx NomItem ."
    private static readonly Regex OtherPlayerLostRegex = new Regex(
        @"^([\p{L}0-9\-_'’ ]+?)\s+a perdu\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "Vous avez détruit Xx NomItem ."
    private static readonly Regex PlayerDestroyedRegex = new Regex(
        @"Vous avez détruit\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "[Pseudo] a détruit Xx NomItem ."
    private static readonly Regex OtherPlayerDestroyedRegex = new Regex(
        @"^([\p{L}0-9\-_'’ ]+?)\s+a détruit\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "Vous avez brisé Xx NomItem ."
    private static readonly Regex PlayerBrokeRegex = new Regex(
        @"Vous avez brisé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "[Pseudo] a brisé Xx NomItem ."
    private static readonly Regex OtherPlayerBrokeRegex = new Regex(
        @"^([\p{L}0-9\-_'’ ]+?)\s+a brisé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "Vous avez supprimé Xx NomItem ."
    private static readonly Regex PlayerDeletedRegex = new Regex(
        @"Vous avez supprimé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "[Pseudo] a supprimé Xx NomItem ."
    private static readonly Regex OtherPlayerDeletedRegex = new Regex(
        @"^([\p{L}0-9\-_'' ]+?)\s+a supprimé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour dépôt en coffre: "Vous avez déposé Xx NomItem dans votre coffre ."
    private static readonly Regex PlayerDepositedChestRegex = new Regex(
        @"Vous avez déposé\s+(\d+)x\s+(.+?)\s+(?:dans votre coffre|en coffre)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex pour "[Pseudo] a déposé Xx NomItem dans son coffre ."
    private static readonly Regex OtherPlayerDepositedChestRegex = new Regex(
        @"^([\p{L}0-9\-_'’ ]+?)\s+a déposé\s+(\d+)x\s+(.+?)\s+(?:dans son coffre|en coffre)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex générique pour "Vous avez déposé Xx NomItem"
    private static readonly Regex PlayerDepositedGenericRegex = new Regex(
        @"Vous avez déposé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    // Regex générique pour "[Pseudo] a déposé Xx NomItem"
    private static readonly Regex OtherPlayerDepositedGenericRegex = new Regex(
        @"^([\p{L}0-9\-_'’ ]+?)\s+a déposé\s+(\d+)x\s+(.+?)\s*\.\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    
    public event EventHandler<LootItem>? LootItemAdded;
    public event EventHandler<LootItem>? LootItemUpdated;
    public event EventHandler<LootItem>? LootItemRemoved;
    
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
        // Augmenter légèrement le délai pour s'assurer que l'écriture est terminée
        System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => ReadNewLines());
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
                int linesProcessed = 0;
                while (true)
                {
                    line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    ProcessLine(line);
                    linesProcessed++;
                }
                
                _lastPosition = reader.BaseStream.Position;
                
                if (linesProcessed > 0)
                {
                    Logger.Debug("LootTracker", $"{linesProcessed} ligne(s) traitée(s) depuis la position {_lastPosition - (linesProcessed * 50)}");
                }
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
        
        // Vérifier "Vous avez perdu Xx NomItem" EN PREMIER (pattern principal utilisé par Wakfu pour destruction/bris/dépôt)
        match = PlayerLostRegex.Match(cleanLine);
        if (match.Success)
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            string characterName = _mainCharacterName ?? "Vous";
            Logger.Debug("LootTracker", $"Item perdu détecté: {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les retraits/destructions pour "Vous"
        match = PlayerRemovedRegex.Match(cleanLine);
        if (match.Success)
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            string characterName = _mainCharacterName ?? "Vous";
            Logger.Debug("LootTracker", $"Item retiré détecté: {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        match = PlayerDestroyedRegex.Match(cleanLine);
        if (match.Success)
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            string characterName = _mainCharacterName ?? "Vous";
            Logger.Debug("LootTracker", $"Item détruit détecté: {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les bris d'items pour "Vous"
        match = PlayerBrokeRegex.Match(cleanLine);
        if (match.Success)
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            string characterName = _mainCharacterName ?? "Vous";
            Logger.Debug("LootTracker", $"Item brisé détecté: {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les suppressions d'items pour "Vous"
        match = PlayerDeletedRegex.Match(cleanLine);
        if (match.Success)
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            string characterName = _mainCharacterName ?? "Vous";
            Logger.Debug("LootTracker", $"Item supprimé détecté: {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les dépôts en coffre pour "Vous" (spécifique coffre)
        match = PlayerDepositedChestRegex.Match(cleanLine);
        if (match.Success)
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            string characterName = _mainCharacterName ?? "Vous";
            Logger.Debug("LootTracker", $"Item déposé en coffre détecté: {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les dépôts génériques pour "Vous" (si le pattern spécifique n'a pas matché)
        match = PlayerDepositedGenericRegex.Match(cleanLine);
        if (match.Success && (cleanLine.Contains("coffre", StringComparison.OrdinalIgnoreCase) || 
                             cleanLine.Contains("havre", StringComparison.OrdinalIgnoreCase) ||
                             cleanLine.Contains("déposé", StringComparison.OrdinalIgnoreCase)))
        {
            int quantity = int.Parse(match.Groups[1].Value);
            string itemName = match.Groups[2].Value.Trim();
            string characterName = _mainCharacterName ?? "Vous";
            Logger.Debug("LootTracker", $"Item déposé (générique) détecté: {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les retraits/destructions pour les autres personnages
        match = OtherPlayerRemovedRegex.Match(cleanLine);
        if (match.Success)
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            Logger.Debug("LootTracker", $"Item retiré détecté (autre): {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier "[Pseudo] a perdu Xx NomItem"
        match = OtherPlayerLostRegex.Match(cleanLine);
        if (match.Success)
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            Logger.Debug("LootTracker", $"Item perdu détecté (autre): {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        match = OtherPlayerDestroyedRegex.Match(cleanLine);
        if (match.Success)
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            Logger.Debug("LootTracker", $"Item détruit détecté (autre): {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les bris d'items pour les autres personnages
        match = OtherPlayerBrokeRegex.Match(cleanLine);
        if (match.Success)
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            Logger.Debug("LootTracker", $"Item brisé détecté (autre): {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les suppressions d'items pour les autres personnages
        match = OtherPlayerDeletedRegex.Match(cleanLine);
        if (match.Success)
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            Logger.Debug("LootTracker", $"Item supprimé détecté (autre): {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les dépôts en coffre pour les autres personnages
        match = OtherPlayerDepositedChestRegex.Match(cleanLine);
        if (match.Success)
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            Logger.Debug("LootTracker", $"Item déposé en coffre détecté (autre): {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Vérifier les dépôts génériques pour les autres personnages
        match = OtherPlayerDepositedGenericRegex.Match(cleanLine);
        if (match.Success && (cleanLine.Contains("coffre", StringComparison.OrdinalIgnoreCase) || 
                             cleanLine.Contains("havre", StringComparison.OrdinalIgnoreCase) ||
                             cleanLine.Contains("déposé", StringComparison.OrdinalIgnoreCase)))
        {
            string characterName = match.Groups[1].Value.Trim();
            int quantity = int.Parse(match.Groups[2].Value);
            string itemName = match.Groups[3].Value.Trim();
            Logger.Debug("LootTracker", $"Item déposé (générique) détecté (autre): {characterName}: {itemName} x{quantity}");
            ProcessRemoval(characterName, itemName, quantity);
            return;
        }
        
        // Log les lignes contenant des mots-clés mais non reconnues pour déboguer
        if (cleanLine.Contains("retiré", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("détruit", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("brisé", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("supprimé", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("déposé", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("coffre", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("havre", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("perdu", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Debug("LootTracker", $"Ligne non reconnue (potentiel retrait/dépôt/perte): {cleanLine}");
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
    
    private void ProcessRemoval(string characterName, string itemName, int quantity)
    {
        string key = $"{characterName}|{itemName}";
        
        lock (_lockObject)
        {
            if (_items.TryGetValue(key, out LootItem? existingItem))
            {
                bool shouldRemove = existingItem.RemoveQuantity(quantity);
                
                if (shouldRemove && !existingItem.IsFavorite)
                {
                    // Retirer l'item seulement s'il n'est pas favoris
                    _items.Remove(key);
                    LootItemRemoved?.Invoke(this, existingItem);
                    Logger.Debug("LootTracker", $"Item retiré/détruit: {characterName}: {itemName} x{quantity} (quantité atteint 0)");
                }
                else if (shouldRemove && existingItem.IsFavorite)
                {
                    // Garder l'item mais mettre la quantité à 0
                    Logger.Debug("LootTracker", $"Item favoris retiré/détruit (conservé): {characterName}: {itemName} x{quantity} (quantité = 0 mais favoris)");
                    LootItemUpdated?.Invoke(this, existingItem);
                }
                else
                {
                    // Mise à jour de la quantité
                    Logger.Debug("LootTracker", $"Quantité mise à jour: {characterName}: {itemName} - retiré: {quantity}, nouvelle quantité: {existingItem.Quantity}");
                    LootItemUpdated?.Invoke(this, existingItem);
                }
            }
            else
            {
                Logger.Debug("LootTracker", $"Tentative de retrait d'un item non trouvé: {characterName}: {itemName} x{quantity}");
            }
        }
    }
    
    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}

