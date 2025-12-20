using System;
using System.Collections.Generic;

namespace GameOverlay.Kikimeter.Models;

/// <summary>
/// Contexte de combat pour tracker l'état actuel (lanceur de sort, invocations, etc.)
/// </summary>
public class CombatContext
{
    /// <summary>
    /// Dernier joueur à avoir lancé un sort
    /// </summary>
    public string? CurrentCaster { get; set; }
    
    /// <summary>
    /// Joueur précédent qui avait lancé un sort (pour détecter les changements de tour)
    /// </summary>
    public string? PreviousCaster { get; set; }
    
    /// <summary>
    /// Dernier sort lancé
    /// </summary>
    public string? CurrentSpell { get; set; }
    
    /// <summary>
    /// Indique si le dernier sort est une invocation
    /// </summary>
    public bool IsSummonSpell { get; set; }
    
    /// <summary>
    /// Propriétaire de la prochaine invocation en attente d'instanciation
    /// </summary>
    public string? PendingSummonOwner { get; set; }
    
    /// <summary>
    /// Dernier propriétaire ayant lancé un sort d'invocation (fallback pour traits passifs)
    /// </summary>
    public string? LastSummonOwner { get; set; }
    
    /// <summary>
    /// Dictionnaire associant l'ID d'une invocation à son propriétaire
    /// </summary>
    public Dictionary<long, string> SummonOwnership { get; set; }
    
    /// <summary>
    /// Dictionnaire pour mapper les noms d'invocations à leur ID (pour les invocations homonymes)
    /// </summary>
    public Dictionary<string, long> SummonNameToId { get; set; }
    
    /// <summary>
    /// Timestamp du dernier sort lancé (pour gérer les rebonds/multi-cibles)
    /// Permet d'attribuer les dégâts au bon lanceur même s'il y a un délai entre les impacts
    /// </summary>
    public DateTime LastSpellCastTime { get; set; }
    
    /// <summary>
    /// Délai maximum (en secondes) pour considérer qu'un dégât appartient au dernier sort lancé
    /// Utile pour les sorts avec rebonds ou multi-cibles qui peuvent avoir des délais entre les impacts
    /// </summary>
    public const double SpellDamageWindowSeconds = 5.0;
    
    /// <summary>
    /// Historique des sorts récents pour gérer les sorts spammés (AoE rapides)
    /// Structure: (Caster, Spell, Timestamp)
    /// </summary>
    public List<(string Caster, string Spell, DateTime Timestamp)> RecentSpells { get; set; }
    
    /// <summary>
    /// Nombre maximum de sorts à garder dans l'historique
    /// </summary>
    public const int MaxRecentSpells = 3;

    /// <summary>
    /// Durée de vie des effets appliqués (pour associer les dégâts indirects)
    /// </summary>
    public static readonly TimeSpan EffectOwnershipLifetime = TimeSpan.FromSeconds(18);

    /// <summary>
    /// Durée de vie étendue pour l'effet Courroux (dégâts différés des Iops).
    /// </summary>
    public static readonly TimeSpan CourrouxEffectOwnershipLifetime = TimeSpan.FromSeconds(35);

    /// <summary>
    /// Effets appliqués avec leur propriétaire
    /// </summary>
    public Dictionary<string, EffectOwnership> EffectOwnerships { get; set; }

    /// <summary>
    /// Lignes de log non attribuées (diagnostic)
    /// </summary>
    public List<string> UnaccountedLines { get; set; }
    
    public CombatContext()
    {
        SummonOwnership = new Dictionary<long, string>();
        SummonNameToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        LastSpellCastTime = DateTime.MinValue;
        RecentSpells = new List<(string, string, DateTime)>();
        EffectOwnerships = new Dictionary<string, EffectOwnership>(StringComparer.OrdinalIgnoreCase);
        UnaccountedLines = new List<string>();
    }
    
    /// <summary>
    /// Réinitialise le contexte pour un nouveau combat
    /// </summary>
    public void Reset()
    {
        CurrentCaster = null;
        PreviousCaster = null;
        CurrentSpell = null;
        IsSummonSpell = false;
        PendingSummonOwner = null;
        LastSummonOwner = null;
        LastSpellCastTime = DateTime.MinValue;
        RecentSpells.Clear();
        SummonOwnership.Clear();
        SummonNameToId.Clear();
        EffectOwnerships.Clear();
        UnaccountedLines.Clear();
    }

    private static string CreateEffectKey(string effectName, string? target)
    {
        return string.IsNullOrEmpty(target)
            ? effectName
            : $"{effectName}::{target}";
    }

    public void RegisterEffectOwnership(string effectName, string owner, string? target, DateTime timestamp, TimeSpan? customLifetime = null)
    {
        if (string.IsNullOrWhiteSpace(effectName) || string.IsNullOrWhiteSpace(owner))
            return;

        string keyWithTarget = CreateEffectKey(effectName, target);
        var lifetime = customLifetime ?? GetEffectLifetime(effectName);
        EffectOwnerships[keyWithTarget] = new EffectOwnership(effectName, owner, target, timestamp, lifetime);

        // Enregistrer également une entrée générique sans cible pour couverture globale
        string genericKey = CreateEffectKey(effectName, null);
        EffectOwnerships[genericKey] = new EffectOwnership(effectName, owner, null, timestamp, lifetime);
    }

    public string? TryResolveEffectOwner(string effectName, string? target, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(effectName))
            return null;

        string keyWithTarget = CreateEffectKey(effectName, target);
        if (EffectOwnerships.TryGetValue(keyWithTarget, out var targetedOwnership))
        {
            if (!targetedOwnership.IsExpired(timestamp))
            {
                targetedOwnership.Refresh(timestamp);
                return targetedOwnership.Owner;
            }
            EffectOwnerships.Remove(keyWithTarget);
        }

        string genericKey = CreateEffectKey(effectName, null);
        if (EffectOwnerships.TryGetValue(genericKey, out var genericOwnership))
        {
            if (!genericOwnership.IsExpired(timestamp))
            {
                genericOwnership.Refresh(timestamp);
                return genericOwnership.Owner;
            }
            EffectOwnerships.Remove(genericKey);
        }

        return null;
    }

    private static TimeSpan GetEffectLifetime(string effectName)
    {
        if (string.Equals(effectName, "Courroux", StringComparison.OrdinalIgnoreCase))
        {
            return CourrouxEffectOwnershipLifetime;
        }

        return EffectOwnershipLifetime;
    }

    public string? GetRecentCaster(TimeSpan window)
    {
        var now = DateTime.Now;
        for (int i = RecentSpells.Count - 1; i >= 0; i--)
        {
            var entry = RecentSpells[i];
            if (now - entry.Timestamp <= window)
            {
                return entry.Caster;
            }
        }
        return null;
    }
}

