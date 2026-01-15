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
    private readonly LogParser _logParser;
    private const string LogCategory = "LogParserPlayerDataProvider";

    public LogParserPlayerDataProvider(LogParser logParser)
    {
        _logParser = logParser ?? throw new ArgumentNullException(nameof(logParser));
    }

    public Dictionary<string, PlayerData> GetCurrentPlayers()
    {
        var result = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Récupérer les joueurs depuis le LogParser
            foreach (var kvp in _logParser.PlayerStats)
            {
                var playerStats = kvp.Value;
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
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Récupérer les joueurs actuellement en combat depuis le LogParser
            // Le LogParser maintient une liste _currentCombatPlayers, mais elle n'est pas exposée
            // On utilise donc les PlayerStats qui ont été mis à jour récemment
            var recentThreshold = DateTime.Now.AddSeconds(-60); // Joueurs vus dans les 60 dernières secondes

            foreach (var kvp in _logParser.PlayerStats)
            {
                var playerStats = kvp.Value;
                
                // Considérer comme en combat si le joueur a été vu récemment
                if (playerStats.LastSeenInCombat >= recentThreshold || 
                    _logParser.CurrentState == CombatState.Active)
                {
                    result.Add(playerStats.Name);
                    result.Add(playerStats.PlayerId.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la récupération des joueurs en combat: {ex.Message}");
        }

        return result;
    }

    public bool IsCombatActive => _logParser.CurrentState == CombatState.Active;
}
