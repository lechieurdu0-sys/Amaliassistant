using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameOverlay.Kikimeter.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service pour surveiller et détecter les ventes en temps réel depuis wakfu_chat.log
/// </summary>
public class SaleTracker : IDisposable
{
    private readonly string _logFilePath;
    private readonly object _lockObject = new object();
    private long _lastPosition = 0;
    private long _lastKnownFileLength = 0;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly HashSet<string> _processedSaleLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastCleanupTime = DateTime.Now;
    private const int CleanupIntervalMinutes = 5; // Nettoyer les lignes traitées toutes les 5 minutes
    private int _consecutiveFailures = 0; // Compteur de tentatives consécutives échouées
    private const int MaxConsecutiveFailures = 10; // Après 10 échecs consécutifs, forcer une lecture complète
    private bool _isReading = false;
    
    // Regex pour détecter les informations de vente dans les nouvelles lignes
    private static readonly Regex[] SaleInfoPatterns = new[]
    {
        // Pattern 1: "Vous avez vendu X objets pour un prix total de Y§" (peut avoir du texte après le §)
        new Regex(
            @"Vous\s+avez\s+vendu\s+(\d+)\s+(?:objets?|items?)\s+pour\s+(?:un\s+)?(?:prix\s+)?total\s+de\s+(\d+(?:\s+\d+)*)\s*§",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 2: "Vous avez vendu X items pour un total de Y kamas"
        new Regex(
            @"Vous\s+avez\s+vendu\s+(\d+)\s+(?:objets?|items?)\s+(?:pour\s+un\s+total\s+de\s+)?(\d+(?:\s+\d+)*)\s+(?:§|kamas?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 3: "X items vendus pour Y kamas" ou "X objets vendus pour Y§"
        new Regex(
            @"(\d+)\s+(?:objets?|items?)\s+vendus?\s+(?:pour\s+)?(?:un\s+)?(?:prix\s+)?total\s+de\s+?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 4: "X items vendus. Total : Y kamas" ou "X items vendus, total Y kamas"
        new Regex(
            @"(\d+)\s+(?:objets?|items?)\s+vendus?[.,]\s*(?:Total\s*[:]?\s*)?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 5: Recherche séparée des nombres (objets/items et kamas/§ dans n'importe quel ordre)
        new Regex(
            @"(\d+)\s+(?:objets?|items?).*?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)|(\d+(?:\s+\d+)*)\s*(?:§|kamas?).*?(\d+)\s+(?:objets?|items?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 6: Plus flexible - cherche "vendu" + nombre + "objets/items" + nombre + "§/kamas" dans n'importe quel ordre
        new Regex(
            @".*vendu.*?(\d+).*?(?:objets?|items?).*?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)|.*vendu.*?(\d+(?:\s+\d+)*)\s*(?:§|kamas?).*?(\d+).*?(?:objets?|items?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 7: Très flexible - cherche simplement un nombre, "objets/items", un autre nombre, "§/kamas" dans une ligne contenant "vendu"
        new Regex(
            @"(?=.*vendu)(?=.*(?:objets?|items?))(?=.*(?:§|kamas?)).*?(\d+).*?(?:objets?|items?).*?(\d+(?:\s+\d+)*)\s*(?:§|kamas?)|(?=.*vendu)(?=.*(?:objets?|items?))(?=.*(?:§|kamas?)).*?(\d+(?:\s+\d+)*)\s*(?:§|kamas?).*?(\d+).*?(?:objets?|items?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    };
    
    public event EventHandler<SaleInfo>? SaleDetected;
    
    public SaleTracker(string logFilePath)
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
                    _lastKnownFileLength = fileInfo.Length;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("SaleTracker", $"Erreur lors de l'initialisation: {ex.Message}");
            }
        }
        else
        {
            Logger.Info("SaleTracker", $"Fichier de log n'existe pas encore: {_logFilePath} - La surveillance attendra sa création");
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
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    // Augmenter la taille du buffer interne pour éviter de manquer des événements
                    InternalBufferSize = 65536 // 64 KB au lieu de 8 KB par défaut
                };
                
                _fileWatcher.Changed += (sender, e) =>
                {
                    // Vérifier que c'est bien notre fichier
                    if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Réveil optionnel : déclencher une lecture immédiate (mais le polling continue)
                        Logger.Debug("SaleTracker", $"Événement FileSystemWatcher détecté (réveil optionnel): {e.FullPath}");
                        System.Threading.Tasks.Task.Run(() => PollAndRead());
                    }
                };
                _fileWatcher.Created += (sender, e) =>
                {
                    if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info("SaleTracker", $"Fichier de log créé détecté (réveil optionnel): {e.FullPath}");
                        _lastPosition = 0;
                        _lastKnownFileLength = 0;
                        System.Threading.Tasks.Task.Run(() => PollAndRead());
                    }
                };
                _fileWatcher.Error += OnFileWatcherError;
                _fileWatcher.EnableRaisingEvents = true;
                
                Logger.Info("SaleTracker", $"FileSystemWatcher initialisé pour surveiller le dossier {directory} (fichier cible: {fileName})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la création du FileSystemWatcher: {ex.Message}");
        }
        
        Logger.Info("SaleTracker", $"SaleTracker initialisé pour: {_logFilePath}");
        
        // Lire les dernières lignes au démarrage pour détecter les ventes récentes seulement si le fichier existe
        if (File.Exists(_logFilePath))
        {
            // Délai augmenté pour laisser le temps au fichier de se stabiliser
            System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ => 
            {
                Logger.Info("SaleTracker", "Démarrage de la lecture des lignes récentes...");
                ReadRecentLines(100);
            });
        }
    }
    
    /// <summary>
    /// Lit les dernières lignes du fichier pour détecter les ventes récentes au démarrage
    /// </summary>
    private void ReadRecentLines(int maxLines = 100)
    {
        if (!File.Exists(_logFilePath))
        {
            Logger.Warning("SaleTracker", $"Fichier de log non trouvé pour ReadRecentLines: {_logFilePath}");
            return;
        }
        
        lock (_lockObject)
        {
            try
            {
                long fileLength = 0;
                var lines = new System.Collections.Generic.List<string>();
                
                // Lire toutes les lignes et garder la position de fin
                using (var reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                        // Garder seulement les N dernières lignes en mémoire
                        if (lines.Count > maxLines)
                        {
                            lines.RemoveAt(0);
                        }
                    }
                    // Récupérer la position finale (fin du fichier)
                    fileLength = reader.BaseStream.Length;
                }
                
                if (lines.Count == 0)
                {
                    Logger.Warning("SaleTracker", "Aucune ligne à lire dans le fichier de log");
                    // Mettre à jour _lastPosition même si aucune ligne
                    _lastPosition = fileLength;
                    return;
                }
                
                Logger.Info("SaleTracker", $"Lecture des {lines.Count} dernières lignes au démarrage pour détecter les ventes récentes");
                
                // Parcourir toutes les lignes et utiliser ProcessLine() pour la déduplication
                // ProcessLine() invoquera automatiquement l'événement SaleDetected si une vente est détectée
                int linesProcessed = 0;
                int linesWithKeywords = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    string rawLine = lines[i];
                    
                    // Utiliser ProcessLine() au lieu d'appeler directement ParseSaleInfo()
                    // Cela garantit la déduplication et la cohérence avec ReadNewLines()
                    // ProcessLine() vérifie déjà si la ligne a été traitée et invoque l'événement si nécessaire
                    ProcessLine(rawLine);
                    linesProcessed++;
                    
                    // Log pour diagnostic (vérifier les lignes contenant des mots-clés)
                    string cleanLine = ProcessLineForParsing(rawLine);
                    if (cleanLine.Contains("vendu", StringComparison.OrdinalIgnoreCase) || 
                        cleanLine.Contains("items", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.Contains("objets", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.Contains("kamas", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.Contains("§", StringComparison.Ordinal))
                    {
                        linesWithKeywords++;
                    }
                }
                
                // IMPORTANT : Mettre à jour _lastPosition à la fin du fichier
                // pour que ReadNewLines() ne relise pas ces lignes
                _lastPosition = fileLength;
                Logger.Info("SaleTracker", $"ReadRecentLines terminé. {linesProcessed} ligne(s) traitée(s), _lastPosition mis à jour à {_lastPosition} (fin du fichier)");
                
                if (linesWithKeywords > 0)
                {
                    Logger.Info("SaleTracker", $"{linesWithKeywords} ligne(s) avec mots-clés de vente analysée(s) sur {lines.Count} ligne(s) totales");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("SaleTracker", $"Erreur lors de la lecture des lignes récentes: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
    
    /// <summary>
    /// Nettoie une ligne pour le parsing (extrait le texte sans les préfixes)
    /// </summary>
    private static string ProcessLineForParsing(string line)
    {
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
        return cleanLine;
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
                Logger.Info("SaleTracker", $"Fichier recréé/tronqué détecté (polling) - ancienne taille={_lastPosition}, nouvelle taille={currentLength}");
                _lastPosition = 0;
                _lastKnownFileLength = 0;
                // Relire depuis le début
                ReadNewLines();
            }
            
            _lastKnownFileLength = currentLength;
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors du polling: {ex.Message}");
        }
    }
    
    private void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        Logger.Error("SaleTracker", $"Erreur du FileSystemWatcher: {e.GetException().Message}");
        
        // Réinitialiser le FileSystemWatcher en cas d'erreur
        try
        {
            if (_fileWatcher != null)
            {
                Logger.Info("SaleTracker", "Tentative de réinitialisation du FileSystemWatcher après erreur...");
                
                // Désactiver temporairement
                _fileWatcher.EnableRaisingEvents = false;
                
                // Attendre un peu avant de réactiver
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    try
                    {
                        if (_fileWatcher != null)
                        {
                            _fileWatcher.EnableRaisingEvents = true;
                            Logger.Info("SaleTracker", "FileSystemWatcher réinitialisé et réactivé après erreur");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("SaleTracker", $"Erreur lors de la réactivation du FileSystemWatcher: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la gestion de l'erreur du FileSystemWatcher: {ex.Message}");
        }
        
        // Le timer continuera à fonctionner même si le FileSystemWatcher échoue
        // Cela garantit qu'on ne rate pas de ventes
    }
    
    private void ReadNewLines()
    {
        if (!File.Exists(_logFilePath))
        {
            // Si le fichier n'existe pas encore, réinitialiser la position pour qu'il soit lu depuis le début quand il sera créé
            _lastPosition = 0;
            Logger.Debug("SaleTracker", "Fichier de log n'existe pas encore, lecture ignorée");
            return;
        }
        
        // Éviter les lectures simultanées
        if (_isReading)
            return;
        
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
                Logger.Info("SaleTracker", $"Fichier de log réinitialisé (taille réduite). Reprise de la lecture depuis le début. Ancienne position={_lastPosition}, nouvelle taille={fileLength}");
                _lastPosition = 0;
            }

            if (_lastPosition < 0)
            {
                _lastPosition = 0;
            }

            // Se positionner à la dernière position connue
            reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
            
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                // Pas de nouvelles lignes
                return;
            }
            
            // Lire ligne par ligne jusqu'à EOF
            string? line;
            int linesRead = 0;
            while ((line = reader.ReadLine()) != null)
            {
                ProcessLine(line);
                linesRead++;
            }
            
            // Mettre à jour lastPosition
            _lastPosition = reader.BaseStream.Position;
            
            if (linesRead > 0)
            {
                Logger.Debug("SaleTracker", $"{linesRead} nouvelle(s) ligne(s) lue(s) depuis la position {_lastPosition - (linesRead * 50)}");
                _consecutiveFailures = 0; // Réinitialiser le compteur en cas de succès
            }
            
            _lastKnownFileLength = fileLength;
        }
        catch (IOException ioEx)
        {
            // Fichier verrouillé par le jeu - c'est normal, on réessayera au prochain tick
            Logger.Debug("SaleTracker", $"Fichier verrouillé (normal), réessai au prochain tick: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            // Problème de permissions - log mais ne pas bloquer
            Logger.Warning("SaleTracker", $"Problème de permissions: {uaEx.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la lecture: {ex.Message}");
            _consecutiveFailures++;
            if (_consecutiveFailures >= MaxConsecutiveFailures)
            {
                Logger.Warning("SaleTracker", $"Échecs consécutifs après {MaxConsecutiveFailures} tentatives. Le fichier est peut-être verrouillé en permanence.");
                _consecutiveFailures = 0; // Réinitialiser pour éviter les logs répétés
            }
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
        // Créer un hash de la ligne pour la déduplication (sans le timestamp)
        string lineHash = CreateLineHash(line);
        
        // Vérifier si cette ligne a déjà été traitée
        lock (_lockObject)
        {
            if (_processedSaleLines.Contains(lineHash))
            {
                Logger.Debug("SaleTracker", "Ligne de vente déjà traitée, ignorée");
                return;
            }
        }
        
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
        
        // Log pour les lignes contenant des mots-clés de vente (utiliser Info pour le diagnostic)
        bool hasSaleKeywords = cleanLine.Contains("vendu", StringComparison.OrdinalIgnoreCase) || 
                              cleanLine.Contains("items", StringComparison.OrdinalIgnoreCase) ||
                              cleanLine.Contains("objets", StringComparison.OrdinalIgnoreCase) ||
                              cleanLine.Contains("kamas", StringComparison.OrdinalIgnoreCase) ||
                              cleanLine.Contains("§", StringComparison.Ordinal);
        
        if (hasSaleKeywords)
        {
            Logger.Info("SaleTracker", $"Ligne contenant des mots-clés de vente détectée: {cleanLine}");
        }
        
        // Chercher des informations de vente dans la ligne
        var saleInfo = ParseSaleInfo(cleanLine);
        if (saleInfo != null)
        {
            // Marquer cette ligne comme traitée AVANT d'invoquer l'événement pour éviter les doublons
            lock (_lockObject)
            {
                _processedSaleLines.Add(lineHash);
            }
            
            Logger.Info("SaleTracker", $"Vente détectée: {saleInfo.ItemCount} items pour {saleInfo.TotalKamas} kamas");
            SaleDetected?.Invoke(this, saleInfo);
            
            // Nettoyer les anciennes entrées périodiquement pour éviter que le HashSet ne grossisse trop
            CleanupProcessedLinesIfNeeded();
        }
        else if (hasSaleKeywords)
        {
            // Si la ligne contient des mots-clés mais n'a pas été parsée, logger un avertissement
            Logger.Warning("SaleTracker", $"Ligne avec mots-clés de vente détectée mais non parsée: {cleanLine}");
        }
    }
    
    /// <summary>
    /// Crée un hash unique d'une ligne de log en enlevant le timestamp
    /// </summary>
    private static string CreateLineHash(string line)
    {
        // Enlever le timestamp et les préfixes pour créer un hash unique du contenu
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
        
        // Retourner la ligne nettoyée comme hash (simple mais efficace pour la déduplication)
        return cleanLine;
    }
    
    /// <summary>
    /// Nettoie périodiquement le HashSet des lignes traitées pour éviter qu'il ne grossisse trop
    /// </summary>
    private void CleanupProcessedLinesIfNeeded()
    {
        lock (_lockObject)
        {
            var now = DateTime.Now;
            if ((now - _lastCleanupTime).TotalMinutes >= CleanupIntervalMinutes)
            {
                // Garder seulement les 100 dernières lignes pour éviter une croissance infinie
                if (_processedSaleLines.Count > 100)
                {
                    var itemsToKeep = _processedSaleLines.TakeLast(50).ToList();
                    _processedSaleLines.Clear();
                    foreach (var item in itemsToKeep)
                    {
                        _processedSaleLines.Add(item);
                    }
                    Logger.Debug("SaleTracker", $"Nettoyage du cache des lignes traitées: {_processedSaleLines.Count} lignes conservées");
                }
                _lastCleanupTime = now;
            }
        }
    }
    
    /// <summary>
    /// Parse une ligne pour extraire les informations de vente
    /// </summary>
    private static SaleInfo? ParseSaleInfo(string cleanLine)
    {
        // Essayer chaque pattern jusqu'à trouver une correspondance
        int patternIndex = 0;
        foreach (var pattern in SaleInfoPatterns)
        {
            var match = pattern.Match(cleanLine);
            if (match.Success)
            {
                Logger.Info("SaleTracker", $"Pattern {patternIndex} a matché! Groups.Count = {match.Groups.Count}");
                int itemCount = 0;
                long totalKamas = 0;
                
                // Pour les patterns avec ordre variable (4, 5, 6, 7 - indices 4, 5, 6 car 0-indexed), les groupes peuvent être dans un ordre différent
                if (patternIndex >= 4)
                {
                    // Pattern avec ordre variable : items puis kamas OU kamas puis items
                    if (match.Groups[1].Success && match.Groups[2].Success)
                    {
                        // Ordre: items puis kamas
                        if (int.TryParse(match.Groups[1].Value, out itemCount))
                        {
                            string kamasStr = match.Groups[2].Value;
                            Logger.Debug("SaleTracker", $"Kamas string brut (ordre variable): '{kamasStr}'");
                            kamasStr = kamasStr.Replace(" ", "").Replace("\u00A0", "").Replace("\u2009", "").Replace("\u202F", "");
                            Logger.Debug("SaleTracker", $"Kamas string nettoyé (ordre variable): '{kamasStr}'");
                            if (!long.TryParse(kamasStr, out totalKamas))
                            {
                                Logger.Warning("SaleTracker", $"Échec du parsing de kamas (ordre variable): '{kamasStr}'");
                            }
                        }
                    }
                    else if (match.Groups[3].Success && match.Groups[4].Success)
                    {
                        // Ordre: kamas puis items
                        string kamasStr = match.Groups[3].Value;
                        Logger.Debug("SaleTracker", $"Kamas string brut (ordre inversé): '{kamasStr}'");
                        kamasStr = kamasStr.Replace(" ", "").Replace("\u00A0", "").Replace("\u2009", "").Replace("\u202F", "");
                        Logger.Debug("SaleTracker", $"Kamas string nettoyé (ordre inversé): '{kamasStr}'");
                        if (long.TryParse(kamasStr, out totalKamas))
                        {
                            int.TryParse(match.Groups[4].Value, out itemCount);
                        }
                        else
                        {
                            Logger.Warning("SaleTracker", $"Échec du parsing de kamas (ordre inversé): '{kamasStr}'");
                        }
                    }
                }
                else
                {
                    // Patterns normaux : groupe 1 = items, groupe 2 = kamas
                    if (match.Groups.Count >= 3)
                    {
                        if (int.TryParse(match.Groups[1].Value, out itemCount))
                        {
                            // Parser le montant en kamas (peut contenir des espaces comme séparateurs de milliers)
                            string kamasStr = match.Groups[2].Value;
                            Logger.Debug("SaleTracker", $"Kamas string brut: '{kamasStr}'");
                            // Enlever tous les types d'espaces (normaux, insécables, etc.)
                            kamasStr = kamasStr.Replace(" ", "").Replace("\u00A0", "").Replace("\u2009", "").Replace("\u202F", "");
                            Logger.Debug("SaleTracker", $"Kamas string nettoyé: '{kamasStr}'");
                            if (!long.TryParse(kamasStr, out totalKamas))
                            {
                                Logger.Warning("SaleTracker", $"Échec du parsing de kamas: '{kamasStr}'");
                            }
                        }
                    }
                }
                
                if (itemCount > 0 && totalKamas > 0)
                {
                    return new SaleInfo(itemCount, totalKamas);
                }
                else
                {
                    Logger.Warning("SaleTracker", $"Pattern {patternIndex} a matché mais parsing échoué: itemCount={itemCount}, totalKamas={totalKamas}");
                }
            }
            patternIndex++;
        }
        
        // Log seulement si la ligne contient des mots-clés de vente
        if (cleanLine.Contains("vendu", StringComparison.OrdinalIgnoreCase) || 
            cleanLine.Contains("items", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("objets", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("kamas", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("§", StringComparison.Ordinal))
        {
            Logger.Warning("SaleTracker", $"Aucun pattern n'a matché la ligne nettoyée: '{cleanLine}'");
        }
        return null;
    }
    
    /// <summary>
    /// Force la lecture des nouvelles lignes (utile pour l'actualisation en temps réel via un timer)
    /// Cette méthode est appelée périodiquement pour s'assurer de ne rien rater, même si le FileSystemWatcher échoue
    /// POLLING ROBUSTE : source de vérité principale
    /// </summary>
    public void ManualRead()
    {
        // Ne pas bloquer le thread principal, mais s'assurer que la lecture se fait
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                // Utiliser le polling robuste au lieu de ReadNewLines directement
                PollAndRead();
            }
            catch (Exception ex)
            {
                Logger.Error("SaleTracker", $"Erreur dans ManualRead: {ex.Message}");
            }
        });
    }
    
    /// <summary>
    /// Vérifie que le FileSystemWatcher est toujours actif et le réactive si nécessaire
    /// </summary>
    private void VerifyWatcherStatus()
    {
        try
        {
            if (_fileWatcher != null)
            {
                bool wasInactive = !_fileWatcher.EnableRaisingEvents;
                
                if (wasInactive)
                {
                    Logger.Warning("SaleTracker", "FileSystemWatcher n'est plus actif, réactivation...");
                    
                    // Réinitialiser complètement le watcher si possible
                    var directory = Path.GetDirectoryName(_logFilePath);
                    var fileName = Path.GetFileName(_logFilePath);
                    
                    if (directory != null && File.Exists(_logFilePath))
                    {
                        try
                        {
                            // Désactiver et réactiver pour réinitialiser l'état interne
                            _fileWatcher.EnableRaisingEvents = false;
                            System.Threading.Thread.Sleep(10); // Petit délai pour s'assurer que la désactivation est prise en compte
                            _fileWatcher.EnableRaisingEvents = true;
                            Logger.Info("SaleTracker", "FileSystemWatcher réactivé avec succès");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("SaleTracker", $"Erreur lors de la réactivation du FileSystemWatcher: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Si le fichier n'existe pas encore, juste réactiver le watcher pour qu'il détecte la création
                        _fileWatcher.EnableRaisingEvents = true;
                        Logger.Info("SaleTracker", "FileSystemWatcher réactivé (en attente de création du fichier)");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la vérification du statut du FileSystemWatcher: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}


