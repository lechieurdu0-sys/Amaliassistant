using System;
using System.Collections.Generic;
using System.Linq;
using GameOverlay.Kikimeter.Core;
using GameOverlay.Kikimeter.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Implémentation de IPlayerDataProvider basée sur le LogParser actuel
/// Cette classe sert de pont entre l'ancien système (parsing de logs) et le nouveau système (polling JSON)
/// À terme, cette classe sera remplacée par une implémentation basée sur le polling JSON
/// </summary>
public class LogParserPlayerDataProvider : IPlayerDataProvider
{
    private readonly Core.LogParser _logParser;
    private const string LogCategory = "LogParserPlayerDataProvider";

    public LogParserPlayerDataProvider(Core.LogParser logParser)
    {
        _logParser = logParser ?? throw new ArgumentNullException(nameof(logParser));
    }

    public Dictionary<string, PlayerData> GetCurrentPlayers()
    {
        var result = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Récupérer les joueurs depuis le LogParser
            foreach (var playerStats in _logParser.GetPlayerSnapshot())
            {
                var playerData = new PlayerData
                {
                    Id = playerStats.PlayerId.ToString(),
                    Name = playerStats.Name,
                    IsMainCharacter = playerStats.IsMainCharacter,
                    IsInGroup = playerStats.IsInGroup,
                    LastSeenInCombat = playerStats.LastSeenInCombat != DateTime.MinValue 
                        ? playerStats.LastSeenInCombat 
                        : DateTime.Now,
                    IsActive = playerStats.IsActive
                };

                result[playerData.Id] = playerData;
                result[playerData.Name] = playerData; // Permet la recherche par nom aussi
            }
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la récupération des joueurs: {ex.Message}");
        }

        return result;
    }

    public HashSet<string> GetPlayersInCombat()
    {
        return IsCombatActive ? _logParser.GetCombatPlayerKeys() : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsCombatActive => _logParser.CurrentState == CombatState.Active;
}
