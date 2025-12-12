using System;
using System.IO;
using System.Text.RegularExpressions;
using GameOverlay.Kikimeter.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service pour lire et parser les informations de vente depuis wakfu_chat.log
/// La première ligne du log contient généralement les informations de vente lors de la connexion
/// </summary>
public class SaleNotificationService
{
    private const string LogCategory = "SaleNotificationService";
    
    // Regex pour parser les informations de vente
    // Formats possibles :
    // - "Vous avez vendu X items pour un total de Y kamas"
    // - "X items vendus pour Y kamas"
    // - "X items vendus. Total : Y kamas"
    // - etc.
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
    
    /// <summary>
    /// Lit la première ligne du fichier de log et extrait les informations de vente
    /// </summary>
    public static SaleInfo? ReadSaleInfoFromFirstLine(string logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath) || !File.Exists(logFilePath))
        {
            Logger.Debug(LogCategory, $"Fichier de log non trouvé ou chemin invalide: {logFilePath}");
            return null;
        }
        
        try
        {
            using var reader = new StreamReader(new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            string? firstLine = reader.ReadLine();
            
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                Logger.Debug(LogCategory, "Première ligne du log est vide");
                return null;
            }
            
            Logger.Debug(LogCategory, $"Première ligne du log: {firstLine}");
            
            // Nettoyer la ligne (enlever les préfixes timestamp et [Information (jeu)])
            string cleanLine = firstLine;
            const string infoMarker = "[Information (jeu)] ";
            int infoIndex = firstLine.IndexOf(infoMarker, StringComparison.OrdinalIgnoreCase);
            if (infoIndex >= 0)
            {
                cleanLine = firstLine[(infoIndex + infoMarker.Length)..].Trim();
            }
            else
            {
                int fallbackIndex = firstLine.LastIndexOf("] ", StringComparison.Ordinal);
                if (fallbackIndex >= 0)
                {
                    cleanLine = firstLine[(fallbackIndex + 2)..].Trim();
                }
            }
            
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
                                string kamasStr = match.Groups[2].Value.Replace(" ", "").Replace("\u00A0", ""); // Enlever espaces et espaces insécables
                                long.TryParse(kamasStr, out totalKamas);
                            }
                        }
                    }
                    
                    if (itemCount > 0 && totalKamas > 0)
                    {
                        Logger.Info(LogCategory, $"Informations de vente détectées: {itemCount} items pour {totalKamas} kamas");
                        return new SaleInfo(itemCount, totalKamas);
                    }
                }
            }
            
            Logger.Debug(LogCategory, $"Aucune information de vente trouvée dans la première ligne: {cleanLine}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la lecture de la première ligne du log: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Modèle pour les informations de vente
/// </summary>
public class SaleInfo
{
    public int ItemCount { get; }
    public long TotalKamas { get; }
    
    public SaleInfo(int itemCount, long totalKamas)
    {
        ItemCount = itemCount;
        TotalKamas = totalKamas;
    }
}

