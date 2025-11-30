using System;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Résout les informations d'affichage pour les classes (nom normalisé, icône).
/// </summary>
internal static class ClassResourceProvider
{
    private const string ResourcePrefix = "pack://application:,,,/GameOverlay.Kikimeter;component/Resources/Assets/Classes/";

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eliotrope"] = "Eliotrope",
        ["iop"] = "Iop",
        ["sram"] = "Sram",
        ["cra"] = "Cra",
        ["sacrieur"] = "Sacrieur",
        ["ecaflip"] = "Écaflip",
        ["ouginak"] = "Ouginak",
        ["feca"] = "Féca",
        ["osamodas"] = "Osamodas",
        ["enutrof"] = "Enutrof",
        ["xelor"] = "Xélor",
        ["eniripsa"] = "Eniripsa",
        ["sadida"] = "Sadida",
        ["pandawa"] = "Pandawa",
        ["roublard"] = "Roublard",
        ["zobal"] = "Zobal",
        ["steamer"] = "Steamer",
        ["huppermage"] = "Huppermage"
    };

    private static readonly Dictionary<string, string> IconFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eliotrope"] = "eliotrope.png",
        ["iop"] = "iop.png",
        ["sram"] = "sram.png",
        ["cra"] = "cra.png",
        ["sacrieur"] = "sacrieur.png",
        ["ecaflip"] = "ecaflip.png",
        ["ouginak"] = "ouginak.png",
        ["feca"] = "feca.png",
        ["osamodas"] = "osamodas.png",
        ["enutrof"] = "enutrof.png",
        ["xelor"] = "xelor.png",
        ["eniripsa"] = "eniripsa.png",
        ["sadida"] = "sadida.png",
        ["pandawa"] = "pandawa.png",
        ["roublard"] = "roublard.png",
        ["zobal"] = "zobal.png",
        ["steamer"] = "steamer.png",
        ["huppermage"] = "huppermage.png"
    };

    private static readonly HashSet<string> LoggedIcons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedMisses = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object IconCacheLock = new();

    public static string GetDisplayName(string? rawClassName)
    {
        if (string.IsNullOrWhiteSpace(rawClassName))
            return string.Empty;

        var normalized = Normalize(rawClassName);
        if (DisplayNames.TryGetValue(normalized, out var display))
            return display;

        return rawClassName.Trim();
    }

    public static string? GetIconUri(string? rawClassName)
    {
        if (string.IsNullOrWhiteSpace(rawClassName))
            return null;

        var normalized = Normalize(rawClassName);
        if (!IconFiles.TryGetValue(normalized, out var fileName))
        {
            if (LoggedMisses.Add(normalized))
            {
                Logger.Debug(nameof(ClassResourceProvider), $"Icône non trouvée pour la classe '{rawClassName}' (normalisée '{normalized}').");
            }
            return null;
        }

        if (LoggedIcons.Add(normalized))
        {
            Logger.Info(nameof(ClassResourceProvider), $"Icône résolue pour la classe '{rawClassName}' → '{fileName}'.");
        }

        return $"{ResourcePrefix}{fileName}";
    }

    public static ImageSource? GetIconImage(string? rawClassName)
    {
        if (string.IsNullOrWhiteSpace(rawClassName))
            return null;

        var normalized = Normalize(rawClassName);
        lock (IconCacheLock)
        {
            if (IconCache.TryGetValue(normalized, out var cached))
            {
                return cached;
            }
        }

        var uri = GetIconUri(rawClassName);
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelHeight = 64;
            bitmap.UriSource = new Uri(uri, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            lock (IconCacheLock)
            {
                IconCache[normalized] = bitmap;
            }
            return bitmap;
        }
        catch (Exception ex)
        {
            Logger.Warning(nameof(ClassResourceProvider), $"Impossible de charger l'icône '{uri}' pour '{rawClassName}': {ex.Message}");
            lock (IconCacheLock)
            {
                IconCache[normalized] = null;
            }
            return null;
        }
    }

    /// <summary>
    /// Charge l'icône de classe directement depuis le numéro de breed.
    /// Les images doivent être nommées selon le format: "{breed}.png"
    /// </summary>
    public static ImageSource? GetIconImageFromBreed(string? breedValue)
    {
        if (string.IsNullOrWhiteSpace(breedValue))
            return null;

        if (!int.TryParse(breedValue.Trim(), out var breedId))
            return null;

        return GetIconImageFromBreed(breedId);
    }

    /// <summary>
    /// Charge l'icône de classe directement depuis le numéro de breed.
    /// Les images doivent être nommées selon le format: "{breed}.png"
    /// </summary>
    public static ImageSource? GetIconImageFromBreed(int breedId)
    {
        var cacheKey = $"breed_{breedId}";
        lock (IconCacheLock)
        {
            if (IconCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        var fileName = $"{breedId}.png";
        var uri = $"{ResourcePrefix}{fileName}";

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelHeight = 64;
            bitmap.UriSource = new Uri(uri, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            lock (IconCacheLock)
            {
                IconCache[cacheKey] = bitmap;
            }
            return bitmap;
        }
        catch (Exception ex)
        {
            Logger.Warning(nameof(ClassResourceProvider), $"Impossible de charger l'icône '{uri}' pour breed '{breedId}': {ex.Message}");
            lock (IconCacheLock)
            {
                IconCache[cacheKey] = null;
            }
            return null;
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}


