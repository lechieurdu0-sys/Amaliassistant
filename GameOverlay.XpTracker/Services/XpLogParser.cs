using System;
using System.Text.RegularExpressions;
using GameOverlay.XpTracker.Models;

namespace GameOverlay.XpTracker.Services;

/// <summary>
/// Parse les lignes de log Wakfu pour extraire les gains d'expérience.
/// </summary>
public sealed class XpLogParser
{
    private static readonly Regex XpRegex = new(
        @"\[Information\s+\((?<context>combat|jeu)\)\]\s+(?<name>.+?)\s*:\s*\+(?<xp>[\d\s\u00A0\u202F\u2007\u2009]+)\s+points d'XP\.?(?:\s+Prochain niveau dans\s*:\s*(?<remaining>[\d\s\u00A0\u202F\u2007\u2009]+)\.?)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Analyse une ligne de log et produit un évènement d'expérience.
    /// </summary>
    public bool TryParse(string line, out XpGainEvent? xpEvent)
    {
        xpEvent = null;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var normalized = LogTextUtilities.NormalizeLine(line);
        if (string.IsNullOrEmpty(normalized))
            return false;

        var match = XpRegex.Match(normalized);
        if (!match.Success)
            return false;

        var name = match.Groups["name"].Value.Trim();
        if (string.IsNullOrEmpty(name))
            return false;

        var xpValue = LogTextUtilities.ParseLong(match.Groups["xp"].Value);
        if (xpValue is null)
            return false;

        var remainingGroup = match.Groups["remaining"];
        var remaining = remainingGroup.Success
            ? LogTextUtilities.ParseLong(remainingGroup.Value)
            : null;

        var context = match.Groups["context"].Value;
        var isCombat = string.Equals(context, "combat", StringComparison.OrdinalIgnoreCase);

        xpEvent = new XpGainEvent(
            entityName: name,
            experienceGained: xpValue.Value,
            experienceToNextLevel: remaining,
            isCombatExperience: isCombat,
            timestamp: DateTime.Now,
            rawLine: normalized);

        return true;
    }
}



