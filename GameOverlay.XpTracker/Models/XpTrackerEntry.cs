using System;

namespace GameOverlay.XpTracker.Models;

/// <summary>
/// Représente l'état courant de l'expérience pour un personnage suivi.
/// </summary>
public sealed class XpTrackerEntry
{
    public string EntityName { get; init; } = string.Empty;

    /// <summary>
    /// Expérience totale accumulée pendant la session courante.
    /// </summary>
    public long TotalExperienceGained { get; set; }

    /// <summary>
    /// Expérience restante avant le prochain niveau (dernière information connue).
    /// </summary>
    public long? ExperienceToNextLevel { get; set; }

    /// <summary>
    /// Date de la dernière mise à jour.
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// Nombre d'événements de gain reçus.
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Indique si l'expérience provient d'un combat (true) ou d'une autre source (ex: métier).
    /// </summary>
    public bool IsCombatExperience { get; set; }
}



