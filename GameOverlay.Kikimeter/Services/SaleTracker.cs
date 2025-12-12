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
        // Pattern 1: "Vous avez vendu X items pour un total de Y kamas"
        new Regex(
            @"Vous\s+avez\s+vendu\s+(\d+)\s+items?\s+(?:pour\s+un\s+total\s+de\s+)?(\d+(?:\s+\d+)*)\s+kamas?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 2: "X items vendus pour Y kamas"
        new Regex(
            @"(\d+)\s+items?\s+vendus?\s+(?:pour\s+)?(?:un\s+total\s+de\s+)?(\d+(?:\s+\d+)*)\s+kamas?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 3: "X items vendus. Total : Y kamas" ou "X items vendus, total Y kamas"
        new Regex(
            @"(\d+)\s+items?\s+vendus?[.,]\s*(?:Total\s*[:]?\s*)?(\d+(?:\s+\d+)*)\s+kamas?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        
        // Pattern 4: Recherche séparée des nombres (items et kamas dans n'importe quel ordre)
        new Regex(
            @"(\d+)\s+items?.*?(\d+(?:\s+\d+)*)\s+kamas?|(\d+(?:\s+\d+)*)\s+kamas?.*?(\d+)\s+items?",
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
        System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => ReadNewLines());
    }
    
    private void ReadNewLines()
    {
        if (!File.Exists(_logFilePath))
            return;
        
        lock (_lockObject)
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
                Logger.Error("SaleTracker", $"Erreur lors de la lecture: {ex.Message}");
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
        foreach (var pattern in SaleInfoPatterns)
        {
            var match = pattern.Match(cleanLine);
            if (match.Success)
            {
                int itemCount = 0;
                long totalKamas = 0;
                
                // Pour le pattern 4, les groupes peuvent être dans un ordre différent
                if (pattern == SaleInfoPatterns[3])
                {
                    // Pattern avec ordre variable : items puis kamas OU kamas puis items
                    if (match.Groups[1].Success && match.Groups[2].Success)
                    {
                        // Ordre: items puis kamas
                        if (int.TryParse(match.Groups[1].Value, out itemCount))
                        {
                            string kamasStr = match.Groups[2].Value.Replace(" ", "").Replace("\u00A0", "");
                            long.TryParse(kamasStr, out totalKamas);
                        }
                    }
                    else if (match.Groups[3].Success && match.Groups[4].Success)
                    {
                        // Ordre: kamas puis items
                        string kamasStr = match.Groups[3].Value.Replace(" ", "").Replace("\u00A0", "");
                        if (long.TryParse(kamasStr, out totalKamas))
                        {
                            int.TryParse(match.Groups[4].Value, out itemCount);
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
                            string kamasStr = match.Groups[2].Value.Replace(" ", "").Replace("\u00A0", "");
                            long.TryParse(kamasStr, out totalKamas);
                        }
                    }
                }
                
                if (itemCount > 0 && totalKamas > 0)
                {
                    return new SaleInfo(itemCount, totalKamas);
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Force la lecture des nouvelles lignes (utile pour l'actualisation en temps réel via un timer)
    /// </summary>
    public void ManualRead()
    {
        System.Threading.Tasks.Task.Run(() => ReadNewLines());
    }
    
    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}

