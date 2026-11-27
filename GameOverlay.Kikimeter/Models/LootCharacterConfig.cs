using System;
using System.Collections.Generic;
using System.Linq;
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
    [JsonProperty("removedCharacters")]
    public List<string> RemovedCharacters { get; set; } = new();

    public bool IsRemoved(string? name) => !string.IsNullOrWhiteSpace(name) &&
        (RemovedCharacters?.Contains(name, StringComparer.OrdinalIgnoreCase) ?? false);

    public void Normalize()
    {
        Characters = new Dictionary<string, bool>(
            Characters ?? new Dictionary<string, bool>(), StringComparer.OrdinalIgnoreCase);
        MyCharacters ??= new();
        ManualCharacters ??= new();
        RemovedCharacters ??= new();
        foreach (string name in RemovedCharacters.ToArray())
        {
            Characters.Remove(name);
            MyCharacters.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            ManualCharacters.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(MainCharacter, name, StringComparison.OrdinalIgnoreCase))
                MainCharacter = null;
        }
    }

    public void Forget(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        RemovedCharacters ??= new();
        if (!IsRemoved(name)) RemovedCharacters.Add(name);
        Normalize();
    }

    // Only a new observed presence (or explicit user add) lifts a temporary removal.
    public bool ObservePresence(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        bool changed = IsRemoved(name);
        Allow(name);
        return Characters.TryAdd(name, true) || changed;
    }

    public void Allow(string name)
    {
        RemovedCharacters?.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }

    [JsonProperty("lastUpdate")]
    public string LastUpdate { get; set; } = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

