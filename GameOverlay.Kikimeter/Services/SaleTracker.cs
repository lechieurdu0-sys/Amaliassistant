using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using GameOverlay.Kikimeter.Models;

namespace GameOverlay.Kikimeter.Services;

// Independent sale reader. It never creates or updates a loot inventory.
public sealed class SaleTracker : IDisposable
{
    public string LogFilePath { get; }
    private readonly LogTailCursor _cursor = new();
    private System.Threading.Timer? _timer;
    private bool _opened;
    private bool _missing;
    private DateTime _creation;
    private int _reading;
    private volatile bool _disposed;
    public event EventHandler<SaleInfo>? SaleDetected;

    public SaleTracker(string logFilePath)
    {
        LogFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        try
        {
            using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            _cursor.InitializeAtEnd(stream);
            _creation = File.GetCreationTimeUtc(LogFilePath);
            _opened = true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Start()
    {
        if (!_disposed && _timer == null)
            _timer = new System.Threading.Timer(_ => ManualRead(), null, 0, 300);
    }

    public void ManualRead()
    {
        if (_disposed || Interlocked.Exchange(ref _reading, 1) != 0) return;
        try
        {
            if (!File.Exists(LogFilePath)) { _missing = true; return; }
            using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var creation = File.GetCreationTimeUtc(LogFilePath);
            bool replaced = _opened && (_missing || creation != _creation);
            _opened = true;
            _missing = false;
            _creation = creation;
            _cursor.Read(stream, line =>
            {
                if (_disposed) return;
                var sale = ParseSystemSale(line);
                if (sale == null) return;
                try { SaleDetected?.Invoke(this, sale); }
                catch (Exception ex) { Logger.Warning("SaleTracker", ex.Message); }
            }, () => { }, replaced);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (Exception ex) { Logger.Warning("SaleTracker", ex.Message); }
        finally { Volatile.Write(ref _reading, 0); }
    }

    public static SaleInfo? ParseSystemSale(string line)
    {
        const string marker = "[Information (jeu)] ";
        int index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        string message = line[(index + marker.Length)..].Trim();
        if (!message.Contains("vendu", StringComparison.OrdinalIgnoreCase)) return null;
        return ParseSaleInfo(message);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }

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
    

}
