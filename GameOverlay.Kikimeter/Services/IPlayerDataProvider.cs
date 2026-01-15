using System;
using System.Collections.Generic;
using GameOverlay.Kikimeter.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Interface pour fournir les données des joueurs depuis le polling JSON
/// Cette interface permet de découpler la logique de gestion des joueurs de la source de données
/// </summary>
public interface IPlayerDataProvider
{
    /// <summary>
    /// Récupère tous les joueurs actuellement détectés via le polling JSON
    /// </summary>
    /// <returns>Dictionnaire des joueurs avec leur ID comme clé</returns>
    Dictionary<string, PlayerData> GetCurrentPlayers();

    /// <summary>
    /// Récupère les joueurs actuellement dans un combat
    /// </summary>
    /// <returns>Liste des IDs des joueurs en combat</returns>
    HashSet<string> GetPlayersInCombat();

    /// <summary>
    /// Vérifie si un combat est actuellement actif
    /// </summary>
    bool IsCombatActive { get; }
}

/// <summary>
/// Données d'un joueur issues du polling JSON
/// </summary>
public class PlayerData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsMainCharacter { get; set; }
    public bool IsInGroup { get; set; }
    public DateTime LastSeenInCombat { get; set; }
    public bool IsActive { get; set; }
}
