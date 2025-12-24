using System;
using System.IO;
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
    private readonly FileSystemWatcher? _fileWatcher;
    
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
        
        if (!File.Exists(_logFilePath))
        {
            Logger.Warning("SaleTracker", $"Fichier de log non trouvé: {_logFilePath}");
            return;
        }
        
        // Initialiser la position à la fin du fichier (ne pas lire l'historique)
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
            Logger.Error("SaleTracker", $"Erreur lors de l'initialisation: {ex.Message}");
        }
        
        // Créer le FileSystemWatcher
        try
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            var fileName = Path.GetFileName(_logFilePath);
            
            if (directory != null)
            {
                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    // Augmenter la taille du buffer interne pour éviter de manquer des événements
                    InternalBufferSize = 65536 // 64 KB au lieu de 8 KB par défaut
                };
                
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Error += OnFileWatcherError;
                _fileWatcher.EnableRaisingEvents = true;
                
                Logger.Info("SaleTracker", "FileSystemWatcher configuré avec buffer de 64 KB");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la création du FileSystemWatcher: {ex.Message}");
        }
        
        Logger.Info("SaleTracker", $"SaleTracker initialisé pour: {_logFilePath}");
        
        // Lire les dernières lignes au démarrage pour détecter les ventes récentes
        // Délai augmenté pour laisser le temps au fichier de se stabiliser
        System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ => 
        {
            Logger.Info("SaleTracker", "Démarrage de la lecture des lignes récentes...");
            ReadRecentLines(100);
        });
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
                var lines = new System.Collections.Generic.List<string>();
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
                }
                
                if (lines.Count == 0)
                {
                    Logger.Warning("SaleTracker", "Aucune ligne à lire dans le fichier de log");
                    return;
                }
                
                Logger.Info("SaleTracker", $"Lecture des {lines.Count} dernières lignes au démarrage pour détecter les ventes récentes");
                
                // Parcourir les lignes de la plus récente à la plus ancienne
                // pour trouver la première occurrence d'une vente
                bool saleFound = false;
                int linesWithKeywords = 0;
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    string rawLine = lines[i];
                    string cleanLine = ProcessLineForParsing(rawLine);
                    
                    // Log toutes les lignes contenant des mots-clés de vente
                    if (cleanLine.Contains("vendu", StringComparison.OrdinalIgnoreCase) || 
                        cleanLine.Contains("items", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.Contains("objets", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.Contains("kamas", StringComparison.OrdinalIgnoreCase) ||
                        cleanLine.Contains("§", StringComparison.Ordinal))
                    {
                        linesWithKeywords++;
                        Logger.Info("SaleTracker", $"Ligne analysée ({i+1}/{lines.Count}): {rawLine}");
                        Logger.Info("SaleTracker", $"Ligne nettoyée: {cleanLine}");
                    }
                    
                    var saleInfo = ParseSaleInfo(cleanLine);
                    if (saleInfo != null)
                    {
                        Logger.Info("SaleTracker", $"Vente récente détectée au démarrage: {saleInfo.ItemCount} items pour {saleInfo.TotalKamas} kamas");
                        SaleDetected?.Invoke(this, saleInfo);
                        saleFound = true;
                        break; // Ne traiter que la vente la plus récente
                    }
                }
                
                if (!saleFound)
                {
                    Logger.Warning("SaleTracker", $"Aucune vente trouvée dans les {lines.Count} dernières lignes (lignes avec mots-clés: {linesWithKeywords})");
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
    
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Délai réduit pour une détection plus rapide (25ms au lieu de 50ms)
        // Le FileSystemWatcher peut manquer des événements, donc on s'appuie aussi sur le timer
        System.Threading.Tasks.Task.Delay(25).ContinueWith(_ => ReadNewLines());
    }
    
    private void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        Logger.Error("SaleTracker", $"Erreur du FileSystemWatcher: {e.GetException().Message}");
        // Le timer continuera à fonctionner même si le FileSystemWatcher échoue
        // Cela garantit qu'on ne rate pas de ventes
    }
    
    private void ReadNewLines()
    {
        if (!File.Exists(_logFilePath))
            return;
        
        // Utiliser TryEnter pour éviter les blocages si une autre lecture est en cours
        if (!System.Threading.Monitor.TryEnter(_lockObject, 100))
        {
            // Si on ne peut pas acquérir le lock rapidement, on ignore cette lecture
            // Le timer rappellera bientôt (25ms)
            return;
        }
        
        try
        {
            // Plusieurs tentatives en cas de fichier verrouillé
            int maxRetries = 3;
            int retryDelay = 10;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                    if (_lastPosition > reader.BaseStream.Length)
                    {
                        _lastPosition = 0;
                        Logger.Info("SaleTracker", "Fichier de log réinitialisé (taille réduite). Reprise de la lecture depuis le début.");
                    }

                    reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
                    
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        return; // Pas de nouvelles lignes
                    
                    string? line;
                    int linesRead = 0;
                    while (true)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            break;
                        }

                        ProcessLine(line);
                        linesRead++;
                    }
                    
                    _lastPosition = reader.BaseStream.Position;
                    
                    if (linesRead > 0)
                    {
                        Logger.Debug("SaleTracker", $"{linesRead} nouvelle(s) ligne(s) lue(s)");
                    }
                    
                    // Succès, on sort de la boucle de retry
                    return;
                }
                catch (IOException ioEx) when (attempt < maxRetries - 1)
                {
                    // Fichier verrouillé, on réessaie après un court délai
                    Logger.Debug("SaleTracker", $"Tentative {attempt + 1}/{maxRetries} - Fichier verrouillé, nouvelle tentative dans {retryDelay}ms: {ioEx.Message}");
                    System.Threading.Thread.Sleep(retryDelay);
                    retryDelay *= 2; // Backoff exponentiel
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la lecture après 3 tentatives: {ex.Message}");
        }
        finally
        {
            System.Threading.Monitor.Exit(_lockObject);
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
        
        // Log de debug pour les lignes contenant des mots-clés de vente
        if (cleanLine.Contains("vendu", StringComparison.OrdinalIgnoreCase) || 
            cleanLine.Contains("items", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("objets", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("kamas", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("§", StringComparison.Ordinal))
        {
            Logger.Debug("SaleTracker", $"Ligne analysée: {cleanLine}");
        }
        
        // Chercher des informations de vente dans la ligne
        var saleInfo = ParseSaleInfo(cleanLine);
        if (saleInfo != null)
        {
            Logger.Info("SaleTracker", $"Vente détectée: {saleInfo.ItemCount} items pour {saleInfo.TotalKamas} kamas");
            SaleDetected?.Invoke(this, saleInfo);
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
    /// </summary>
    public void ManualRead()
    {
        // Ne pas bloquer le thread principal, mais s'assurer que la lecture se fait
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                ReadNewLines();
            }
            catch (Exception ex)
            {
                Logger.Error("SaleTracker", $"Erreur dans ManualRead: {ex.Message}");
            }
        });
    }
    
    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}


