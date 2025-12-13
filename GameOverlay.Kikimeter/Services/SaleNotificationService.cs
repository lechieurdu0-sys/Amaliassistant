using System;
using System.Collections.Generic;
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
    // - "Vous avez vendu X objets pour un prix total de Y§ pendant votre absence"
    // - "Vous avez vendu X items pour un total de Y kamas"
    // - "X items vendus pour Y kamas"
    // - etc.
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
            
            return ParseSaleInfoFromLine(firstLine);
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la lecture de la première ligne du log: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Lit les dernières lignes du fichier de log et extrait les informations de vente récentes
    /// Utile pour détecter les ventes qui apparaissent lors de la connexion
    /// </summary>
    public static SaleInfo? ReadSaleInfoFromRecentLines(string logFilePath, int maxLinesToRead = 50)
    {
        if (string.IsNullOrWhiteSpace(logFilePath) || !File.Exists(logFilePath))
        {
            Logger.Debug(LogCategory, $"Fichier de log non trouvé ou chemin invalide: {logFilePath}");
            return null;
        }
        
        try
        {
            // Lire les dernières lignes du fichier
            var lines = new List<string>();
            using (var reader = new StreamReader(new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                    // Garder seulement les N dernières lignes en mémoire
                    if (lines.Count > maxLinesToRead)
                    {
                        lines.RemoveAt(0);
                    }
                }
            }
            
            if (lines.Count == 0)
            {
                Logger.Debug(LogCategory, "Le fichier de log est vide");
                return null;
            }
            
            // Parcourir les lignes de la plus récente à la plus ancienne
            // pour trouver la première occurrence d'une vente
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var saleInfo = ParseSaleInfoFromLine(lines[i]);
                if (saleInfo != null)
                {
                    Logger.Debug(LogCategory, $"Informations de vente trouvées dans la ligne {i + 1} (sur {lines.Count})");
                    return saleInfo;
                }
            }
            
            Logger.Debug(LogCategory, $"Aucune information de vente trouvée dans les {lines.Count} dernières lignes");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la lecture des dernières lignes du log: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Parse une ligne de log pour extraire les informations de vente
    /// </summary>
    private static SaleInfo? ParseSaleInfoFromLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
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
        
        Logger.Debug(LogCategory, $"Ligne nettoyée pour parsing: {cleanLine}");
        
        // Essayer chaque pattern jusqu'à trouver une correspondance
        int patternIndex = 0;
        foreach (var pattern in SaleInfoPatterns)
        {
            var match = pattern.Match(cleanLine);
            Logger.Debug(LogCategory, $"Pattern {patternIndex}: match = {match.Success}");
            if (match.Success)
            {
                Logger.Debug(LogCategory, $"Pattern {patternIndex} a matché! Groups.Count = {match.Groups.Count}");
                int itemCount = 0;
                long totalKamas = 0;
                
                    // Pour les patterns avec ordre variable (pattern 4 et suivants), les groupes peuvent être dans un ordre différent
                    if (patternIndex >= 4)
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
                            string kamasStr = match.Groups[2].Value;
                            Logger.Debug(LogCategory, $"Kamas string brut: '{kamasStr}'");
                            // Enlever tous les types d'espaces (normaux, insécables, etc.)
                            kamasStr = kamasStr.Replace(" ", "").Replace("\u00A0", "").Replace("\u2009", "").Replace("\u202F", "");
                            Logger.Debug(LogCategory, $"Kamas string nettoyé: '{kamasStr}'");
                            if (!long.TryParse(kamasStr, out totalKamas))
                            {
                                Logger.Warning(LogCategory, $"Échec du parsing de kamas: '{kamasStr}'");
                            }
                        }
                    }
                }
                
                if (itemCount > 0 && totalKamas > 0)
                {
                    Logger.Info(LogCategory, $"Informations de vente détectées: {itemCount} items pour {totalKamas} kamas");
                    return new SaleInfo(itemCount, totalKamas);
                }
                else
                {
                    Logger.Warning(LogCategory, $"Pattern {patternIndex} a matché mais parsing échoué: itemCount={itemCount}, totalKamas={totalKamas}");
                }
            }
            patternIndex++;
        }
        
        Logger.Debug(LogCategory, "Aucun pattern n'a matché la ligne nettoyée");
        return null;
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

