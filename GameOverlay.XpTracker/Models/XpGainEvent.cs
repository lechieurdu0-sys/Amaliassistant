using System;

namespace GameOverlay.XpTracker.Models;

/// <summary>
/// Représente un gain d'expérience détecté dans les logs.
/// </summary>
public sealed class XpGainEvent
{
    public XpGainEvent(
        string entityName,
        long experienceGained,
        long? experienceToNextLevel,
        bool isCombatExperience,
        DateTime timestamp,
        string rawLine)
    {
        EntityName = entityName;
        ExperienceGained = experienceGained;
        ExperienceToNextLevel = experienceToNextLevel;
        IsCombatExperience = isCombatExperience;
        Timestamp = timestamp;
        RawLine = rawLine;
    }

    /// <summary>
    /// Nom du personnage (ou de l'entité) ayant reçu l'expérience.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Quantité d'expérience gagnée.
    /// </summary>
    public long ExperienceGained { get; }

    /// <summary>
    /// Expérience restante avant le prochain niveau (si connue).
    /// </summary>
    public long? ExperienceToNextLevel { get; }

    /// <summary>
    /// Indique si le gain provient du contexte combat (versus métier).
    /// </summary>
    public bool IsCombatExperience { get; }

    /// <summary>
    /// Timestamp approximatif basé sur l'heure locale de parsing.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Ligne brute du log qui a généré cet événement.
    /// </summary>
    public string RawLine { get; }
}



