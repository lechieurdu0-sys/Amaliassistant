using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameOverlay.Kikimeter.Models;

/// <summary>
/// Configuration des personnages pour le loot tracker
/// Contient les 6 personnages les plus récents et leur état d'affichage
/// </summary>
public class LootCharacterConfig
{
    /// <summary>
    /// Dictionnaire : Nom du personnage -> Afficher ses loots (true/false)
    /// Maximum 6 personnages détectés automatiquement + ajouts manuels
    /// </summary>
    [JsonProperty("characters")]
    public Dictionary<string, bool> Characters { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Nom du personnage principal (celui qui utilise "Vous avez ramassé")
    /// Détecté automatiquement ou configuré manuellement
    /// </summary>
    [JsonProperty("mainCharacter")]
    public string? MainCharacter { get; set; }
    
    /// <summary>
    /// Liste des personnages qui appartiennent au joueur (maximum 3 : le principal + 2 autres)
    /// </summary>
    [JsonProperty("myCharacters")]
    public List<string> MyCharacters { get; set; } = new List<string>();

    /// <summary>
    /// Liste des personnages ajoutés manuellement par l'utilisateur
    /// </summary>
    [JsonProperty("manualCharacters")]
    public List<string> ManualCharacters { get; set; } = new List<string>();
    
    /// <summary>
    /// Timestamp de la dernière mise à jour
    /// </summary>
    [JsonProperty("lastUpdate")]
    public string LastUpdate { get; set; } = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

