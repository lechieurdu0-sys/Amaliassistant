using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameOverlay.Kikimeter.Models;

/// <summary>
/// Modèle JSON pour les données des joueurs issues du polling
/// </summary>
public class PlayerDataJson
{
    [JsonProperty("players")]
    public List<PlayerDataItem> Players { get; set; } = new List<PlayerDataItem>();

    [JsonProperty("combatActive")]
    public bool CombatActive { get; set; }

    [JsonProperty("lastUpdate")]
    public DateTime LastUpdate { get; set; } = DateTime.Now;

    [JsonProperty("serverName")]
    public string? ServerName { get; set; }
}

/// <summary>
/// Données d'un joueur dans le JSON
/// </summary>
public class PlayerDataItem
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("isMainCharacter")]
    public bool IsMainCharacter { get; set; }

    [JsonProperty("isInGroup")]
    public bool IsInGroup { get; set; }

    [JsonProperty("lastSeenInCombat")]
    public DateTime LastSeenInCombat { get; set; }

    [JsonProperty("isActive")]
    public bool IsActive { get; set; }

    [JsonProperty("playerId")]
    public long? PlayerId { get; set; }
}
