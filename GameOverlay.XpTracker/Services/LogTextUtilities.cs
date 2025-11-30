using System;
using System.Globalization;
using System.Text;

namespace GameOverlay.XpTracker.Services;

internal static class LogTextUtilities
{
    private static readonly char[] SpaceLikeCharacters =
    {
        '\u00A0', // NBSP
        '\u202F', // Narrow no-break space
        '\u2007', // Figure space
        '\u2009', // Thin space
        '\uFEFF'  // BOM
    };

    public static string NormalizeLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormKC);

        foreach (var ch in SpaceLikeCharacters)
        {
            normalized = normalized.Replace(ch, ' ');
        }

        normalized = normalized.Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized;
    }

    public static long? ParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch == '-' || ch == '+')
            {
                builder.Append(ch);
            }
        }

        if (builder.Length == 0)
            return null;

        if (long.TryParse(builder.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }
}



