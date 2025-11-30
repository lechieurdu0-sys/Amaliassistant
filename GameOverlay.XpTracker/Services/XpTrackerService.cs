using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GameOverlay.XpTracker.Models;

namespace GameOverlay.XpTracker.Services;

/// <summary>
/// Service de suivi d'expérience basé sur les logs Wakfu.
/// </summary>
public sealed class XpTrackerService
{
    private readonly XpLogParser _parser = new();
    private readonly ConcurrentDictionary<string, XpTrackerEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<XpGainEvent>? ExperienceGained;

    /// <summary>
    /// Tente de traiter une ligne de log. Retourne true si un gain d'XP a été détecté.
    /// </summary>
    public bool TryProcessLine(string line, out XpGainEvent? xpEvent)
    {
        if (!_parser.TryParse(line, out xpEvent))
        {
            return false;
        }

        var resolvedEvent = xpEvent!;
        var entry = _entries.GetOrAdd(resolvedEvent.EntityName, CreateEntry);
        entry.TotalExperienceGained += resolvedEvent.ExperienceGained;
        entry.ExperienceToNextLevel = resolvedEvent.ExperienceToNextLevel;
        entry.LastUpdate = resolvedEvent.Timestamp;
        entry.EventCount++;
        entry.IsCombatExperience = resolvedEvent.IsCombatExperience;

        ExperienceGained?.Invoke(this, resolvedEvent);
        return true;
    }

    /// <summary>
    /// Renvoie l'état courant pour l'entité donnée (ou null si inconnue).
    /// </summary>
    public XpTrackerEntry? GetEntry(string entityName)
    {
        return _entries.TryGetValue(entityName, out var entry) ? entry : null;
    }

    /// <summary>
    /// Réinitialise le suivi pour une entité spécifique.
    /// </summary>
    public void Reset(string entityName)
    {
        _entries.TryRemove(entityName, out _);
    }

    /// <summary>
    /// Renvoie tous les suivis actifs.
    /// </summary>
    public IReadOnlyCollection<XpTrackerEntry> GetAllEntries()
    {
        return _entries.Values.ToArray();
    }

    /// <summary>
    /// Réinitialise le suivi complet.
    /// </summary>
    public void Reset()
    {
        _entries.Clear();
    }

    private static XpTrackerEntry CreateEntry(string entityName)
    {
        return new XpTrackerEntry
        {
            EntityName = entityName,
            LastUpdate = DateTime.Now
        };
    }
}


