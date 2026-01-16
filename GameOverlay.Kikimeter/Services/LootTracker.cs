using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using GameOverlay.Kikimeter.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service pour surveiller et parser les items ramassés depuis wakfu_chat.log
/// Détecte les loots pour "Vous" et les autres personnages
/// Utilise LootManagementService comme source unique de vérité
/// </summary>
public class LootTracker : IDisposable
{
    private const int PollingIntervalMs = 300; // 300ms entre chaque vérification
    
    private readonly string _logFilePath;
    private readonly LootManagementService? _lootManagementService;
    private readonly Dictionary<string, LootItem> _items = new(StringComparer.OrdinalIgnoreCase); // Dictionnaire temporaire pour compatibilité (sera supprimé)
    private readonly object _lockObject = new object();
    private long _lastPosition = 0;
    private long _lastKnownFileLength = 0;
    private readonly FileSystemWatcher? _fileWatcher;
    private System.Threading.Timer? _pollingTimer;
    private bool _isReading = false;
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
    
    public LootTracker(string logFilePath, LootManagementService? lootManagementService = null)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        _lootManagementService = lootManagementService;
        
        // Initialiser _lastPosition à la fin du fichier si le fichier existe
        // On attendra les nouvelles écritures et on les lira uniquement
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
                _lastPosition = 0;
            }
        }
        else
        {
            _lastPosition = 0;
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
                // Surveiller le dossier, pas le fichier unique
                _fileWatcher = new FileSystemWatcher(directory)
                {
                    Filter = "*.log",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                
                _fileWatcher.Changed += (sender, e) =>
                {
                    // Vérifier que c'est bien notre fichier
                    if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Réveil optionnel : déclencher une lecture immédiate (mais le polling continue)
                        Logger.Debug("LootTracker", $"Événement FileSystemWatcher détecté (réveil optionnel): {e.FullPath}");
                        System.Threading.Tasks.Task.Run(() => PollAndRead());
                    }
                };
                _fileWatcher.Created += (sender, e) =>
                {
                    if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info("LootTracker", $"Fichier de log créé détecté (réveil optionnel): {e.FullPath}");
                        _lastPosition = 0;
                        _lastKnownFileLength = 0;
                        System.Threading.Tasks.Task.Run(() => PollAndRead());
                    }
                };
                _fileWatcher.EnableRaisingEvents = true;
                Logger.Info("LootTracker", $"FileSystemWatcher initialisé pour surveiller le dossier {directory} (fichier cible: {fileName})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("LootTracker", $"Erreur lors de la création du FileSystemWatcher: {ex.Message}");
        }
        
        // POLLING ROBUSTE : source de vérité principale
        // Ne dépend plus de FileSystemWatcher - le polling lit toujours les données
        _pollingTimer = new System.Threading.Timer(PollingTimerCallback, null, PollingIntervalMs, PollingIntervalMs);
        Logger.Info("LootTracker", $"Polling robuste démarré (intervalle: {PollingIntervalMs}ms) - source de vérité principale");
        
        if (File.Exists(_logFilePath))
        {
            try
            {
                var fileInfo = new FileInfo(_logFilePath);
                _lastKnownFileLength = fileInfo.Length;
            }
            catch
            {
                // Ignorer
            }
        }
        
        Logger.Info("LootTracker", $"LootTracker initialisé pour: {_logFilePath}");
    }
    
    /// <summary>
    /// Callback du timer de polling - méthode principale de lecture
    /// </summary>
    private void PollingTimerCallback(object? state)
    {
        PollAndRead();
    }
    
    /// <summary>
    /// Méthode de polling robuste : vérifie FileInfo.Length et lit si nécessaire
    /// </summary>
    private void PollAndRead()
    {
        // Éviter les lectures simultanées
        if (_isReading)
            return;
        
        // Vérifier que le fichier existe
        if (!File.Exists(_logFilePath))
        {
            // Le fichier n'existe pas encore, on attend
            return;
        }
        
        try
        {
            // Lire FileInfo.Length pour détecter les changements
            var fileInfo = new FileInfo(_logFilePath);
            long currentLength = fileInfo.Length;
            
            // Si Length > lastPosition, il y a de nouvelles données à lire
            if (currentLength > _lastPosition)
            {
                // Ouvrir, lire, fermer - ne jamais garder le FileStream ouvert
                ReadNewLines();
            }
            // Si Length < lastPosition, le fichier a été recréé ou tronqué
            else if (currentLength < _lastPosition)
            {
                Logger.Info("LootTracker", $"Fichier recréé/tronqué détecté (polling) - ancienne taille={_lastPosition}, nouvelle taille={currentLength}");
                _lastPosition = 0;
                _lastKnownFileLength = 0;
                // Relire depuis le début
                ReadNewLines();
            }
            
            _lastKnownFileLength = currentLength;
        }
        catch (Exception ex)
        {
            Logger.Error("LootTracker", $"Erreur lors du polling: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Lit les nouvelles lignes du fichier (ouvre, lit, ferme)
    /// </summary>
    private void ReadNewLines()
    {
        if (!File.Exists(_logFilePath))
        {
            // Si le fichier n'existe pas encore, réinitialiser la position pour qu'il soit lu depuis le début quand il sera créé
            _lastPosition = 0;
            return;
        }
        
        // Éviter les lectures simultanées
        lock (_lockObject)
        {
            if (_isReading)
                return; // Déjà en cours de lecture, ignorer cet appel
            
            _isReading = true;
        }
        
        try
        {
            // Ouvrir le flux (ne jamais le garder ouvert en permanence)
            using var reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var fileLength = reader.BaseStream.Length;

            // Gérer la recréation / rotation du fichier
            if (_lastPosition > fileLength)
            {
                Logger.Info("LootTracker", $"Fichier de log réinitialisé (taille réduite). Reprise de la lecture depuis le début. Ancienne position={_lastPosition}, nouvelle taille={fileLength}");
                _lastPosition = 0;
            }

            if (_lastPosition < 0)
            {
                _lastPosition = 0;
            }

            // Se positionner à la dernière position connue
            reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
            
            // Lire ligne par ligne jusqu'à EOF
            string? line;
            int linesProcessed = 0;
            while ((line = reader.ReadLine()) != null)
            {
                ProcessLine(line);
                linesProcessed++;
            }
            
            // Mettre à jour lastPosition
            _lastPosition = reader.BaseStream.Position;
            
            if (linesProcessed > 0)
            {
                Logger.Debug("LootTracker", $"{linesProcessed} ligne(s) traitée(s) depuis la position {_lastPosition - (linesProcessed * 50)}");
            }
        }
        catch (IOException ioEx)
        {
            // Fichier verrouillé par le jeu - c'est normal, on réessayera au prochain tick
            Logger.Debug("LootTracker", $"Fichier verrouillé (normal), réessai au prochain tick: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            // Problème de permissions - log mais ne pas bloquer
            Logger.Warning("LootTracker", $"Problème de permissions: {uaEx.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error("LootTracker", $"Erreur lors de la lecture: {ex.Message}");
        }
        finally
        {
            lock (_lockObject)
            {
                _isReading = false;
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
        
        // Utiliser LootManagementService si disponible (source unique de vérité)
        if (_lootManagementService != null)
        {
            _lootManagementService.AddOrUpdateLoot(actualCharacterName, itemName, quantity);
            // Les événements seront gérés par le service via la collection ObservableCollection
            return;
        }
        
        // Compatibilité : comportement ancien si pas de service
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
            
            // Si on utilise le service, la mise à jour des noms se fera via le service
            if (_lootManagementService != null)
            {
                // Collecter les items à mettre à jour (copie pour éviter modification pendant itération)
                var itemsToUpdate = _lootManagementService.SessionLoot
                    .Where(i => string.Equals(i.CharacterName, "Vous", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                var itemsToRemove = new List<LootItem>();
                var itemsToAdd = new List<LootItem>();
                
                foreach (var oldItem in itemsToUpdate)
                {
                    // Vérifier si un item avec le nouveau nom existe déjà
                    var existingWithNewName = _lootManagementService.SessionLoot
                        .FirstOrDefault(i => 
                            string.Equals(i.CharacterName, mainCharacterName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(i.ItemName, oldItem.ItemName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingWithNewName != null)
                    {
                        // Fusionner les quantités
                        existingWithNewName.AddQuantity(oldItem.Quantity);
                        // Marquer l'ancien item pour suppression
                        itemsToRemove.Add(oldItem);
                    }
                    else
                    {
                        // Créer un nouvel item avec le nouveau nom
                        var newItem = new LootItem(mainCharacterName, oldItem.ItemName, oldItem.Quantity)
                        {
                            IsFavorite = oldItem.IsFavorite
                        };
                        
                        // Marquer l'ancien pour suppression et le nouveau pour ajout
                        itemsToRemove.Add(oldItem);
                        itemsToAdd.Add(newItem);
                    }
                }
                
                // Appliquer les modifications
                foreach (var item in itemsToRemove)
                {
                    _lootManagementService.SessionLoot.Remove(item);
                }
                
                foreach (var item in itemsToAdd)
                {
                    _lootManagementService.SessionLoot.Add(item);
                }
                
                Logger.Info("LootTracker", $"Personnage principal défini: {mainCharacterName} ({itemsToUpdate.Count} items mis à jour)");
                return;
            }
            
            // Compatibilité : comportement ancien si pas de service
            var itemsToUpdateOld = _items.Where(kvp => string.Equals(kvp.Value.CharacterName, "Vous", StringComparison.OrdinalIgnoreCase)).ToList();
            
            foreach (var kvp in itemsToUpdateOld)
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
        // Utiliser LootManagementService si disponible (source unique de vérité)
        if (_lootManagementService != null)
        {
            _lootManagementService.RemoveLootQuantity(characterName, itemName, quantity);
            // Les événements seront gérés par le service via la collection ObservableCollection
            return;
        }
        
        // Compatibilité : comportement ancien si pas de service
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
        // Arrêter le polling (source de vérité)
        if (_pollingTimer != null)
        {
            _pollingTimer.Dispose();
            _pollingTimer = null;
            Logger.Info("LootTracker", "Polling arrêté");
        }
        
        // Arrêter le FileSystemWatcher (optionnel)
        _fileWatcher?.Dispose();
        Logger.Info("LootTracker", "FileSystemWatcher arrêté");
    }
}

