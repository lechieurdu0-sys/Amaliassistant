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
                    NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite
                };
                
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.EnableRaisingEvents = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la création du FileSystemWatcher: {ex.Message}");
        }
        
        Logger.Info("SaleTracker", $"SaleTracker initialisé pour: {_logFilePath}");
    }
    
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Délai pour éviter les lectures multiples lors d'une écriture en cours
        // Augmenter le délai pour s'assurer que le fichier est complètement écrit
        System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => ReadNewLines());
    }
    
    private void ReadNewLines()
    {
        if (!File.Exists(_logFilePath))
            return;
        
        // Utiliser TryEnter pour éviter les blocages si une autre lecture est en cours
        if (!System.Threading.Monitor.TryEnter(_lockObject, 100))
        {
            Logger.Debug("SaleTracker", "Lecture déjà en cours, skip de cette itération");
            return;
        }
        
        try
        {
            // Réessayer plusieurs fois si le fichier est verrouillé
            int retries = 3;
            for (int retry = 0; retry < retries; retry++)
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
                    {
                        Logger.Debug("SaleTracker", "Pas de nouvelles lignes à lire");
                        return; // Pas de nouvelles lignes
                    }
                    
                    long startPosition = reader.BaseStream.Position;
                    int linesRead = 0;
                    string? line;
                    while (true)
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            break;
                        }

                        linesRead++;
                        ProcessLine(line);
                    }
                    
                    _lastPosition = reader.BaseStream.Position;
                    
                    if (linesRead > 0)
                    {
                        Logger.Debug("SaleTracker", $"Lu {linesRead} nouvelle(s) ligne(s) depuis la position {startPosition}");
                    }
                    
                    return; // Succès, sortir de la boucle de retry
                }
                catch (IOException ioEx) when (ioEx.Message.Contains("partage") || ioEx.Message.Contains("share") || ioEx.Message.Contains("being used"))
                {
                    // Fichier verrouillé, réessayer après un court délai
                    if (retry < retries - 1)
                    {
                        Logger.Debug("SaleTracker", $"Fichier verrouillé, nouvelle tentative dans 50ms (tentative {retry + 1}/{retries})");
                        System.Threading.Thread.Sleep(50);
                        continue;
                    }
                    else
                    {
                        Logger.Warning("SaleTracker", $"Fichier toujours verrouillé après {retries} tentatives: {ioEx.Message}");
                        throw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SaleTracker", $"Erreur lors de la lecture: {ex.Message}\n{ex.StackTrace}");
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
        bool containsSaleKeywords = cleanLine.Contains("vendu", StringComparison.OrdinalIgnoreCase) || 
            cleanLine.Contains("items", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("objets", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("kamas", StringComparison.OrdinalIgnoreCase) ||
            cleanLine.Contains("§", StringComparison.Ordinal);
            
        if (containsSaleKeywords)
        {
            Logger.Debug("SaleTracker", $"Ligne analysée (contient mots-clés): {cleanLine}");
        }
        
        // Chercher des informations de vente dans la ligne
        var saleInfo = ParseSaleInfo(cleanLine);
        if (saleInfo != null)
        {
            Logger.Info("SaleTracker", $"✅ VENTE DÉTECTÉE: {saleInfo.ItemCount} items pour {saleInfo.TotalKamas} kamas (ligne: {cleanLine.Substring(0, Math.Min(100, cleanLine.Length))})");
            SaleDetected?.Invoke(this, saleInfo);
        }
        else if (containsSaleKeywords)
        {
            // Log si on a des mots-clés mais qu'aucun pattern n'a matché
            Logger.Debug("SaleTracker", $"⚠️ Ligne contient des mots-clés de vente mais aucun pattern n'a matché: {cleanLine}");
        }
    }
    
    /// <summary>
    /// Parse une ligne pour extraire les informations de vente
    /// </summary>
    private static SaleInfo? ParseSaleInfo(string cleanLine)
    {
        Logger.Debug("SaleTracker", $"Ligne nettoyée pour parsing: {cleanLine}");
        // Essayer chaque pattern jusqu'à trouver une correspondance
        int patternIndex = 0;
        foreach (var pattern in SaleInfoPatterns)
        {
            var match = pattern.Match(cleanLine);
            Logger.Debug("SaleTracker", $"Pattern {patternIndex}: match = {match.Success}");
            if (match.Success)
            {
                Logger.Debug("SaleTracker", $"Pattern {patternIndex} a matché! Groups.Count = {match.Groups.Count}");
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
        
        Logger.Debug("SaleTracker", "Aucun pattern n'a matché la ligne nettoyée");
        return null;
    }
    
    /// <summary>
    /// Force la lecture des nouvelles lignes (utile pour l'actualisation en temps réel via un timer)
    /// </summary>
    public void ManualRead()
    {
        // Ne pas créer une nouvelle tâche si une lecture est déjà en cours
        // Le timer appelle déjà cette méthode de manière asynchrone
        ReadNewLines();
    }
    
    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}


