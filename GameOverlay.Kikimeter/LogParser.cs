using System;
using System.Text.RegularExpressions;
using GameOverlay.Models;

namespace GameOverlay.Kikimeter;

public static class LogParser
{
    private static readonly Regex KikiPattern = new Regex(
        @"Kiki.*?par\s+(\w+)|Kiki.*?gagné.*?par\s+(\w+)|Kiki.*?obtenu.*?par\s+(\w+)|(\w+).*?a.*?gagné.*?un.*?Kiki|(\w+).*?obtient.*?un.*?Kiki",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LootPattern = new Regex(
        @"Vous avez récupéré\s+(.+)|vous avez obtenu\s+(.+)|vous avez reçu\s+(.+)|loot.*?:\s*(.+)|Vous obtenez\s+(.+)|Vous récupérez\s+(.+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParseKiki(string line, out string? playerName)
    {
        playerName = null;
        var match = KikiPattern.Match(line);
        if (match.Success)
        {
            for (int i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success && !string.IsNullOrWhiteSpace(match.Groups[i].Value))
                {
                    playerName = match.Groups[i].Value.Trim();
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryParseLoot(string line, out string? itemName)
    {
        itemName = null;
        var match = LootPattern.Match(line);
        if (match.Success)
        {
            for (int i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success && !string.IsNullOrWhiteSpace(match.Groups[i].Value))
                {
                    itemName = match.Groups[i].Value.Trim();
                    return true;
                }
            }
        }
        return false;
    }
}





