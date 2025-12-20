using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GameOverlay.Kikimeter;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Kikimeter.Services;

namespace GameOverlay.Kikimeter.Core;

/// <summary>
/// Parseur de logs Wakfu pour extraire les statistiques de combat
/// </summary>
public class LogParser
{
    private const string LogCategory = "LogParser";

    private readonly Dictionary<string, PlayerStats> _playerStats;
    private readonly CombatContext _combatContext;
    private readonly HashSet<string> _currentCombatPlayers;
    private HashSet<string> _playersPendingRemoval;
    private bool _rosterFinalized;
    private CombatState _currentState;

    private static readonly string UnaccountedLogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "logs",
        "kikimeter_untracked.log");
    
    // Expressions régulières pour le parsing (support noms multi-mots)
    private static readonly Regex PlayerJoinRegex = new Regex(
        @".*?\[_FL_\].*?fightId=\d+\s+(.+?)\s+breed\s*:\s*(\d+)\s+\[(\d+)\]\s+isControlledByAI=(true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    private static readonly Regex SpellCastRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?)\s+lance\s+le\s+sort\s+(.+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    private static readonly Regex SummonDeclarationRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+Invoque\s+un\(e\)\s+(.+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    
    // Pattern alternatif pour "Invoque une créature..."
    private static readonly Regex SummonDeclarationAltRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+Invoque\s+une\s+(.+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    
    private static readonly Regex SummonInstantiationRegex = new Regex(
        @"Instanciation\s+d'une\s+nouvelle\s+invocation\s+avec\s+un\s+id\s+de\s+(-?\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    private static readonly Regex SummonJoinRegex = new Regex(
        @".*?\[_FL_\].*?fightId=\d+\s+(.+?)\s+breed\s*:\s*(\d+)\s+\[(-?\d+)\]\s+isControlledByAI=(true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    // Regex améliorée pour capturer les dégâts avec rebonds et autres informations
    // Format: "Cible: -XXX PV (Élément) (Premier rebond)" ou "Cible: -XXX PV (Élément)" ou "Cible: -XXX PV  (Élément) (Premier rebond)"
    // Capture : Groupe 1 = Cible, Groupe 2 = Dégâts, Groupe 3 = Élément (optionnel), Groupe 4 = Info supplémentaire comme "Premier rebond" (optionnel)
    // Gère les espaces multiples entre "PV" et les parenthèses avec \s+ (au moins un espace)
    private static readonly Regex DamageRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+-(\d+)\s+PV\s*(?:\(([^)]+)\))?\s*(?:\(([^)]+)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    private static readonly Regex HealingRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+\+(\d+)\s+PV\s*(?:\(([^)]+)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    private static readonly Regex ShieldRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+(\d+)\s+Armure",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SpaceBetweenDigitsRegex = new Regex(
        @"(?<=\d)[\s\u00A0\u202F\u2007\u2009]+(?=\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MultiSpaceRegex = new Regex(
        @"[ \t]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EffectApplicationRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+([^:]+?)\s+\(([+-]\d+)",
        RegexOptions.Compiled);

    private static readonly Regex NumericWordRegex = new Regex(@"\b\d+\b", RegexOptions.Compiled);

    private static readonly HashSet<string> IgnoredEffectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Parade",
        "Parade !",
        "Coup critique",
        "Dommages infligés",
        "Dommages finaux",
        "Résistance Élémentaire",
        "Tacle",
        "PA",
        "PM",
        "PW",
        "PV",
        "Guerrier joueur"
    };
    
    public event EventHandler<PlayerStats>? PlayerAdded;
    public event EventHandler<string>? PlayerRemoved;
    public event EventHandler? CombatStarted;
    public event EventHandler? CombatEnded;
    public event EventHandler<string>? TurnDetected; // Événement déclenché quand un nouveau tour est détecté (nom du joueur)
    
    public LogParser()
    {
        _playerStats = new Dictionary<string, PlayerStats>(StringComparer.OrdinalIgnoreCase);
        _combatContext = new CombatContext();
        _currentCombatPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _playersPendingRemoval = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _rosterFinalized = true;
        _currentState = CombatState.Waiting;

        Logger.Info(LogCategory, "Initialisation du parseur de logs");

        try
        {
            var directory = Path.GetDirectoryName(UnaccountedLogPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Logger.Debug(LogCategory, $"Création du dossier de logs non suivis: {directory}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Impossible de préparer le fichier de logs non suivis: {ex.Message}");
        }
    }
    
    public CombatState CurrentState => _currentState;
    
    public Dictionary<string, PlayerStats> PlayerStats => _playerStats;
    
    public CombatContext CombatContext => _combatContext;
    
    /// <summary>
    /// Traite une ligne de log
    /// </summary>
    public void ProcessLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        
        var normalizedLine = NormalizeLogLine(line);
        if (!string.IsNullOrEmpty(normalizedLine))
        {
            var preview = normalizedLine.Length > 500 ? normalizedLine.Substring(0, 500) + "…" : normalizedLine;
            Logger.Debug(LogCategory, $"Ligne analysée: {preview}");
        }
        if (string.IsNullOrWhiteSpace(normalizedLine) || ShouldSkipLine(normalizedLine))
            return;

        line = normalizedLine;
        
        try
        {
            // Détection du début de combat
            if (line.Contains("CREATION DU COMBAT", StringComparison.Ordinal))
            {
                HandleCombatStart();
                return;
            }
            
            // Si pas de combat actif, ignorer
            if (_currentState != CombatState.Active)
                return;

            if (TryHandleEffectApplication(line))
                return;
            
            // Détection de fin de combat
            if (line.Contains("[FIGHT] End fight", StringComparison.Ordinal) || 
                line.Contains("Combat terminé", StringComparison.Ordinal) || 
                line.Contains("NetInFight Removed", StringComparison.Ordinal))
            {
                HandleCombatEnd();
                return;
            }
            
            // Identifier les joueurs qui rejoignent
            if (line.Contains("join the fight at", StringComparison.Ordinal) && line.Contains("isControlledByAI=false", StringComparison.Ordinal))
            {
                ParsePlayerJoin(line);
                return;
            }
            
            // Détecter les invocations qui rejoignent (ID négatif + isControlledByAI=true)
            if (line.Contains("join the fight at", StringComparison.Ordinal) && line.Contains("isControlledByAI=true", StringComparison.Ordinal))
            {
                ParseSummonJoin(line);
                return;
            }
            
            // Tracker les sorts lancés
            if (line.Contains("lance le sort", StringComparison.Ordinal))
            {
                ParseSpellCast(line);
                return;
            }
            
            // Détecter les déclarations d'invocation
            if (line.Contains("Invoque un(e)", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("invoque un", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Invoque une", StringComparison.OrdinalIgnoreCase))
            {
                ParseSummonDeclaration(line);
                return;
            }
            
            // Détecter l'instanciation d'invocation
            if (line.Contains("Instanciation d'une nouvelle invocation", StringComparison.Ordinal) || 
                line.Contains("New summon with id", StringComparison.Ordinal))
            {
                ParseSummonInstantiation(line);
                return;
            }
            
            // Traiter les effets de combat (PV, Armure)
            // IMPORTANT: Ne pas réinitialiser CurrentCaster ici pour permettre les rebonds/multi-cibles
            if (line.Contains("PV", StringComparison.Ordinal) || line.Contains("Armure", StringComparison.Ordinal))
            {
                ParseCombatEffect(line);
            }
        }
        catch (Exception ex)
        {
            // Logger l'erreur sans interrompre le parsing
            Logger.Error(LogCategory, $"Erreur parsing ligne: {ex.Message} | Ligne: {line}");
        }
    }

    private static string NormalizeLogLine(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormKC)
                               .Replace('\u00A0', ' ')
                               .Replace('\u202F', ' ')
                               .Replace('\u2007', ' ')
                               .Replace('\u2009', ' ')
                               .Replace('\uFEFF', ' ');

        normalized = SpaceBetweenDigitsRegex.Replace(normalized, string.Empty);
        normalized = MultiSpaceRegex.Replace(normalized, " ");

        return normalized.Trim();
    }

    private static string NormalizeEntityName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormKC)
                               .Replace('\u00A0', ' ')
                               .Replace('\u202F', ' ')
                               .Replace('\u2007', ' ')
                               .Replace('\u2009', ' ')
                               .Replace('\uFEFF', ' ');

        normalized = MultiSpaceRegex.Replace(normalized, " ");

        return normalized.Trim();
    }

    private static bool ShouldSkipLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.TrimStart();

        if (trimmed.StartsWith("at ", StringComparison.Ordinal) ||
            trimmed.StartsWith("at\t", StringComparison.Ordinal) ||
            trimmed.StartsWith("at.", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.StartsWith("java.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("sun.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("org.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Caused by", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Exception", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
    
    private void HandleCombatStart()
    {
        // Toujours réinitialiser lors d'un nouveau combat
        Reset();
        
        _currentState = CombatState.Active;
        Logger.Info(LogCategory, "Début de combat détecté");
        CombatStarted?.Invoke(this, EventArgs.Empty);
    }
    
    private void HandleCombatEnd()
    {
        _currentState = CombatState.Terminated;
        Logger.Info(LogCategory, "Fin de combat détectée");
        CombatEnded?.Invoke(this, EventArgs.Empty);
    }
    
    private void ParsePlayerJoin(string line)
    {
        var match = PlayerJoinRegex.Match(line);
        if (!match.Success)
            return;
        
        string playerName = NormalizeEntityName(match.Groups[1].Value);
        string breed = match.Groups[2].Value.Trim();
        long playerId = long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        
        Logger.Debug(LogCategory, $"ParsePlayerJoin - joueur détecté: '{playerName}' (breed={breed}, id={playerId})");
        
        if (string.IsNullOrEmpty(playerName))
            return;
        
        if (!_playerStats.TryGetValue(playerName, out var stats))
        {
            stats = new PlayerStats
        {
            Name = playerName,
            Breed = breed,
            PlayerId = playerId
        };

            var classFromBreed = BreedClassMapper.GetClassName(breed);
            if (!string.IsNullOrEmpty(classFromBreed))
            {
                stats.ClassName = classFromBreed;
            }
        
        _playerStats[playerName] = stats;
        PlayerAdded?.Invoke(this, stats);
        }
        else
        {
            // Mettre à jour les infos connues
            if (string.IsNullOrEmpty(stats.Breed))
            {
                stats.Breed = breed;
            }
            stats.PlayerId = playerId;

            if (string.IsNullOrEmpty(stats.ClassName))
            {
                var classFromBreed = BreedClassMapper.GetClassName(breed);
                if (!string.IsNullOrEmpty(classFromBreed))
                {
                    stats.ClassName = classFromBreed;
                }
            }
        }

        _currentCombatPlayers.Add(playerName);
        _playersPendingRemoval.Remove(playerName);

        if (_playersPendingRemoval.Count == 0)
        {
            _rosterFinalized = true;
        }
    }
    
    private void ParseSummonJoin(string line)
    {
        var match = SummonJoinRegex.Match(line);
        if (!match.Success)
            return;
        
        string summonName = NormalizeEntityName(match.Groups[1].Value);
        long summonId = long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(summonName))
            return;
        
        Logger.Debug(LogCategory, $"Invocation détectée: {summonName} (ID: {summonId})");
        
        // Les invocations ont des ID négatifs
        if (summonId >= 0)
            return;
        
        // Associer l'invocation à son propriétaire si en attente
        if (_combatContext.PendingSummonOwner != null)
        {
            _combatContext.SummonOwnership[summonId] = _combatContext.PendingSummonOwner;
            _combatContext.SummonNameToId[summonName] = summonId;
            Logger.Debug(LogCategory, $"Invocation associée: {summonName} -> {_combatContext.PendingSummonOwner}");
            _combatContext.PendingSummonOwner = null;
        }
        else
        {
            // Essayer de trouver le propriétaire via le nom (pour les invocations homonymes)
            // En cas d'échec, on pourra faire une association ultérieure
            _combatContext.SummonNameToId[summonName] = summonId;
            Logger.Debug(LogCategory, $"Invocation enregistrée sans propriétaire: {summonName}");
        }
    }
    
    private void ParseSpellCast(string line)
    {
        EnsureRosterFinalized();

        var match = SpellCastRegex.Match(line);
        if (!match.Success)
            return;
        
        string caster = NormalizeEntityName(match.Groups[1].Value);
        string spell = SpellMetadataProvider.NormalizeSpellName(match.Groups[2].Value.Trim());

        if (string.IsNullOrEmpty(caster))
            return;

        if (_playerStats.TryGetValue(caster, out var casterStats))
        {
            var className = SpellMetadataProvider.GetClassForSpell(spell);
            if (!string.IsNullOrEmpty(className) && string.IsNullOrEmpty(casterStats.ClassName))
            {
                casterStats.ClassName = className;
            }
        }
        
        // Détecter un nouveau tour : si le caster change d'un joueur à un autre, c'est un nouveau tour
        bool isNewTurnForCaster = _combatContext.PreviousCaster != caster && 
                                   _combatContext.PreviousCaster != null && 
                                   _combatContext.PreviousCaster != string.Empty;
        
        // Mettre à jour le contexte
        var now = DateTime.Now;
        
        // Ajouter le sort à l'historique (pour gérer les sorts spammés)
        _combatContext.RecentSpells.Add((caster, spell, now));
        
        // Garder seulement les N derniers sorts
        if (_combatContext.RecentSpells.Count > CombatContext.MaxRecentSpells)
        {
            _combatContext.RecentSpells.RemoveAt(0);
        }
        
        _combatContext.CurrentCaster = caster;
        _combatContext.CurrentSpell = spell;
        _combatContext.LastSpellCastTime = now; // Timestamp pour gérer les rebonds
        _combatContext.IsSummonSpell = spell.Contains("Invoque", StringComparison.OrdinalIgnoreCase) || 
                                      spell.Contains("Invocation", StringComparison.OrdinalIgnoreCase) ||
                                      spell.Contains("invoke", StringComparison.OrdinalIgnoreCase);
        
        if (_combatContext.IsSummonSpell)
        {
            _combatContext.PendingSummonOwner = caster;
            _combatContext.LastSummonOwner = caster;
        }
        
        // Compter les tours : quand le caster change vers un nouveau joueur, c'est un nouveau tour
        if (_playerStats.ContainsKey(caster))
        {
            // Si c'est un changement de joueur (nouveau tour), réinitialiser les dégâts du tour et incrémenter
            if (isNewTurnForCaster)
            {
                // Nouveau tour pour le nouveau caster
                _playerStats[caster].NumberOfTurns++;
                _playerStats[caster].ResetTurnDamage(); // Réinitialiser pour le nouveau tour
                Logger.Info(LogCategory, $"Nouveau tour pour {caster} - tours enregistrés: {_playerStats[caster].NumberOfTurns}");
                
                // Enregistrer l'ordre de passage des tours pour l'ordre automatique
                if (!_combatContext.TurnOrder.Contains(caster, StringComparer.OrdinalIgnoreCase))
                {
                    _combatContext.TurnOrder.Add(caster);
                    Logger.Info(LogCategory, $"Ordre de tour enregistré: {caster} (position {_combatContext.TurnOrder.Count - 1})");
                    TurnDetected?.Invoke(this, caster);
                }
            }
            else if (_combatContext.PreviousCaster == null || _combatContext.PreviousCaster == string.Empty)
            {
                // Premier tour de ce joueur dans ce combat
                _playerStats[caster].NumberOfTurns = 1;
                _playerStats[caster].ResetTurnDamage(); // Initialiser à 0 pour le premier tour
                Logger.Info(LogCategory, $"Premier tour pour {caster}");
                
                // Enregistrer l'ordre de passage des tours pour l'ordre automatique
                if (!_combatContext.TurnOrder.Contains(caster, StringComparer.OrdinalIgnoreCase))
                {
                    _combatContext.TurnOrder.Add(caster);
                    Logger.Info(LogCategory, $"Ordre de tour enregistré (premier): {caster} (position {_combatContext.TurnOrder.Count - 1})");
                    TurnDetected?.Invoke(this, caster);
                }
            }
            
            // Mettre à jour PreviousCaster pour la prochaine détection
            _combatContext.PreviousCaster = caster;
        }
    }
    
    private void ParseSummonDeclaration(string line)
    {
        var match = SummonDeclarationRegex.Match(line);
        if (!match.Success)
        {
            // Essayer le pattern alternatif "Invoque une..."
            match = SummonDeclarationAltRegex.Match(line);
        }
        
        if (match.Success)
        {
            string caster = NormalizeEntityName(match.Groups[1].Value);
            string summonName = NormalizeEntityName(match.Groups[2].Value);

            if (string.IsNullOrEmpty(caster))
                return;
            
            _combatContext.CurrentCaster = caster;
            _combatContext.IsSummonSpell = true;
            _combatContext.PendingSummonOwner = caster;
            _combatContext.LastSummonOwner = caster;
        }
        else if (!string.IsNullOrEmpty(_combatContext.CurrentCaster))
        {
            // Fallback : utiliser le dernier lanceur de sort
            _combatContext.PendingSummonOwner = _combatContext.CurrentCaster;
            _combatContext.LastSummonOwner = _combatContext.CurrentCaster;
        }
    }
    
    private void ParseSummonInstantiation(string line)
    {
        var match = SummonInstantiationRegex.Match(line);
        if (!match.Success)
        {
            // Pattern alternatif : "New summon with id -123456"
            match = Regex.Match(line, @"New summon with id (-?\d+)", RegexOptions.CultureInvariant);
        }
        
        if (!match.Success)
            return;
        
        long summonId = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        
        var owner = _combatContext.PendingSummonOwner ?? _combatContext.LastSummonOwner;
        
        if (owner != null)
        {
            _combatContext.SummonOwnership[summonId] = owner;
            Logger.Debug(LogCategory, $"Association fallback invocation: id={summonId} -> {owner}");
        }
    }
    
    private void ParseCombatEffect(string line)
    {
        if (line.Contains("PV", StringComparison.Ordinal))
        {
            if (line.Contains("+", StringComparison.Ordinal))
            {
                ParseHealing(line);
            }
            else if (line.Contains("-", StringComparison.Ordinal))
            {
                ParseDamage(line);
            }
        }
        else if (line.Contains("Armure", StringComparison.Ordinal))
        {
            ParseShield(line);
        }
    }
    
    private void ParseDamage(string line)
    {
        EnsureRosterFinalized();

        var match = DamageRegex.Match(line);
        if (!match.Success)
        {
            Logger.Warning(LogCategory, $"Ligne dégâts ignorée (pas de match regex): {line}");
            RegisterUnaccountedLine(line, "Damage-Regex");
            return;
        }
        
        string target = NormalizeEntityName(match.Groups[1].Value);
        long damage = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var now = DateTime.Now;

        string? effectName = null;
        if (match.Groups.Count > 4)
        {
            effectName = NormalizeEffectName(match.Groups[4].Value);
        }
        
        Logger.Debug(LogCategory, $"ParseDamage - cible='{target}', valeur={damage}, caster courant={_combatContext.CurrentCaster}");
        
        // Dégâts subis par la cible
        if (!string.IsNullOrEmpty(target) && _playerStats.ContainsKey(target))
        {
            _playerStats[target].DamageTaken += damage;
            Logger.Debug(LogCategory, $"{target} dégâts subis cumulés: {_playerStats[target].DamageTaken}");
        }
        else
        {
            Logger.Warning(LogCategory, $"Impossible d'attribuer les dégâts subis à '{target}' (joueur inconnu)");
            RegisterUnaccountedLine(line, "Damage-TargetUnknown");
        }
        
        // Dégâts infligés : déterminer le propriétaire
        string? damageOwner = null;
        if (!string.IsNullOrEmpty(effectName))
        {
            var ownerFromEffect = _combatContext.TryResolveEffectOwner(effectName, target, now);
            if (!string.IsNullOrEmpty(ownerFromEffect))
            {
                damageOwner = ownerFromEffect;
                Logger.Debug(LogCategory, $"Propriétaire déterminé via effet '{effectName}': {damageOwner}");
            }
        }
        
        // PRIORITÉ 1: Vérifier d'abord si CurrentCaster est une invocation (logique originale qui fonctionnait)
        // Cela permet de gérer les invocations correctement avant d'utiliser l'historique des sorts
        if (string.IsNullOrEmpty(damageOwner) && !string.IsNullOrEmpty(_combatContext.CurrentCaster))
        {
            // Si CurrentCaster est une invocation, trouver son propriétaire
            if (IsSummonDamage(_combatContext.CurrentCaster))
            {
                damageOwner = GetDamageOwner(_combatContext.CurrentCaster);
                Logger.Debug(LogCategory, $"Dégâts d'invocation {_combatContext.CurrentCaster} -> propriétaire: {damageOwner}");
                if (damageOwner != null)
                {
                    if (_playerStats.ContainsKey(damageOwner))
                    {
                        _playerStats[damageOwner].DamageDealt += damage;
                        _playerStats[damageOwner].DamageThisTurn += damage;
                        _playerStats[damageOwner].DamageBySummon += damage;
                        Logger.Debug(LogCategory, $"{damageOwner} dégâts (invocation) cumulés: {_playerStats[damageOwner].DamageDealt} | Dégâts invocations: {_playerStats[damageOwner].DamageBySummon}");
                    }
                    return; // Retourner immédiatement pour les invocations (comme avant)
                }
            }
        }
        
        // PRIORITÉ 2: Pour les sorts spammés (AoE rapides), chercher dans l'historique des sorts récents
        // pour trouver le sort le plus récent qui est encore dans sa fenêtre de temps
        (string Caster, string Spell, DateTime Timestamp)? matchingSpell = null;
        
        // Parcourir l'historique des sorts récents (du plus récent au plus ancien)
        if (string.IsNullOrEmpty(damageOwner))
        {
        for (int i = _combatContext.RecentSpells.Count - 1; i >= 0; i--)
        {
            var recentSpell = _combatContext.RecentSpells[i];
            var timeSinceSpell = (now - recentSpell.Timestamp).TotalSeconds;
            
            // Si le sort est dans sa fenêtre de temps, l'utiliser
            if (timeSinceSpell <= CombatContext.SpellDamageWindowSeconds)
            {
                matchingSpell = recentSpell;
                break; // Prendre le plus récent qui est dans la fenêtre
                }
            }
        }
        
        // Si on a trouvé un sort dans la fenêtre, l'utiliser
        if (string.IsNullOrEmpty(damageOwner) && matchingSpell.HasValue)
        {
            damageOwner = matchingSpell.Value.Caster;
            Logger.Debug(LogCategory, $"Dégâts attribués au sort '{matchingSpell.Value.Spell}' de {damageOwner} (delta {(now - matchingSpell.Value.Timestamp).TotalSeconds:F2}s)");
        }
        // Sinon, utiliser CurrentCaster (qui n'est pas une invocation, sinon on serait déjà sorti)
        else if (string.IsNullOrEmpty(damageOwner) && !string.IsNullOrEmpty(_combatContext.CurrentCaster))
        {
            damageOwner = _combatContext.CurrentCaster;
            Logger.Debug(LogCategory, $"Propriétaire des dégâts (joueur): {damageOwner}");
        }
        else
        {
            // Fallback : utiliser GetDamageOwner
            damageOwner ??= GetDamageOwner(target);
            Logger.Debug(LogCategory, $"Propriétaire des dégâts (fallback): {damageOwner}");
        }
        
        // Vérifier si c'est un rebond en analysant les groupes capturés
        string? bounceInfo = match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value) 
            ? match.Groups[4].Value 
            : null;
            
        if (!string.IsNullOrEmpty(bounceInfo) && 
            (bounceInfo.Contains("rebond", StringComparison.OrdinalIgnoreCase) || 
             bounceInfo.Contains("bounce", StringComparison.OrdinalIgnoreCase)))
        {
            Logger.Debug(LogCategory, $"Dégâts avec rebond détecté: {bounceInfo} pour {damageOwner}");
        }
        
        // Attribuer les dégâts au propriétaire
        if (damageOwner != null && _playerStats.ContainsKey(damageOwner))
        {
            _playerStats[damageOwner].DamageDealt += damage;
            _playerStats[damageOwner].DamageThisTurn += damage;
            Logger.Debug(LogCategory, $"{damageOwner} dégâts infligés: {_playerStats[damageOwner].DamageDealt}, DPT courant: {_playerStats[damageOwner].DamagePerTurn}");

            if (!string.IsNullOrEmpty(effectName))
            {
                _combatContext.RegisterEffectOwnership(effectName, damageOwner, target, now);
            }
        }
        else
        {
            RegisterUnaccountedLine(line, "Damage-OwnerUnknown");
            Logger.Warning(LogCategory, $"Impossible d'attribuer les dégâts à un propriétaire: {line}");
        }
    }
    
    private void ParseHealing(string line)
    {
        EnsureRosterFinalized();

        var match = HealingRegex.Match(line);
        if (!match.Success)
        {
            Logger.Warning(LogCategory, $"Ligne soins ignorée (pas de match regex): {line}");
            RegisterUnaccountedLine(line, "Healing-Regex");
            return;
        }
        
        string target = NormalizeEntityName(match.Groups[1].Value);
        long healing = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        
        // Soins reçus par la cible
        if (!string.IsNullOrEmpty(target) && _playerStats.ContainsKey(target))
        {
            // Si le soin vient d'un autre joueur, attribuer au lanceur
            if (_combatContext.CurrentCaster != null && 
                _playerStats.ContainsKey(_combatContext.CurrentCaster) &&
                _combatContext.CurrentCaster != target)
            {
                _playerStats[_combatContext.CurrentCaster].HealingDone += healing;
            }
            else if (_combatContext.CurrentCaster == target)
            {
                // Soins sur soi-même
                _playerStats[target].HealingDone += healing;
            }
        }
        else
        {
            RegisterUnaccountedLine(line, "Healing-TargetUnknown");
            Logger.Warning(LogCategory, $"Impossible d'attribuer un soin (cible inconnue) : {line}");
        }
    }
    
    private void ParseShield(string line)
    {
        EnsureRosterFinalized();

        var match = ShieldRegex.Match(line);
        if (!match.Success)
        {
            Logger.Warning(LogCategory, $"Ligne bouclier ignorée (pas de match regex): {line}");
            RegisterUnaccountedLine(line, "Shield-Regex");
            return;
        }
        
        string target = NormalizeEntityName(match.Groups[1].Value);
        long shield = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        
        // Bouclier donné : attribuer au lanceur du sort
        if (_combatContext.CurrentCaster != null && 
            _playerStats.ContainsKey(_combatContext.CurrentCaster))
        {
            _playerStats[_combatContext.CurrentCaster].ShieldGiven += shield;
        }
        else
        {
            RegisterUnaccountedLine(line, "Shield-OwnerUnknown");
            Logger.Warning(LogCategory, $"Impossible d'attribuer un bouclier : {line}");
        }
    }
    
    /// <summary>
    /// Détermine le propriétaire des dégâts (joueur ou propriétaire de l'invocation)
    /// </summary>
    private string? GetDamageOwner(string entityName)
    {
        var normalizedName = NormalizeEntityName(entityName);

        if (string.IsNullOrEmpty(normalizedName))
            return null;

        // Si c'est un joueur, c'est lui le propriétaire
        if (_playerStats.ContainsKey(normalizedName))
        {
            Logger.Debug(LogCategory, $"{normalizedName} identifié comme joueur");
            return normalizedName;
        }
        
        // Si c'est une invocation, trouver son propriétaire
        if (_combatContext.SummonNameToId.TryGetValue(normalizedName, out long summonId))
        {
            Logger.Debug(LogCategory, $"{normalizedName} identifié comme invocation (ID: {summonId})");
            if (_combatContext.SummonOwnership.TryGetValue(summonId, out string? owner))
            {
                Logger.Debug(LogCategory, $"Propriétaire de l'invocation {summonId} trouvé: {owner}");
                return owner;
            }
            else
            {
                Logger.Warning(LogCategory, $"Aucun propriétaire enregistré pour l'invocation {summonId}");
            }
        }
        else
        {
            Logger.Warning(LogCategory, $"{normalizedName} n'est ni un joueur ni une invocation connue");
        }
        
        // Fallback : utiliser le dernier lanceur de sort
        Logger.Debug(LogCategory, $"Attribution fallback sur caster courant: {_combatContext.CurrentCaster}");
        return _combatContext.CurrentCaster;
    }
    
    /// <summary>
    /// Vérifie si les dégâts proviennent d'une invocation
    /// </summary>
    private bool IsSummonDamage(string entityName)
    {
        var normalizedName = NormalizeEntityName(entityName);

        if (string.IsNullOrEmpty(normalizedName))
            return false;

        // Si l'entité n'est pas un joueur ET qu'elle existe dans SummonNameToId, c'est une invocation
        return !_playerStats.ContainsKey(normalizedName) && 
               _combatContext.SummonNameToId.ContainsKey(normalizedName);
    }

    private bool TryHandleEffectApplication(string line)
    {
        var match = EffectApplicationRegex.Match(line);
        if (!match.Success)
            return false;

        var target = NormalizeEntityName(match.Groups[1].Value);
        var effectRaw = match.Groups[2].Value;
        var effectName = NormalizeEffectName(effectRaw);

        if (string.IsNullOrEmpty(effectName))
            return false;

        var owner = _combatContext.CurrentCaster;
        owner ??= _combatContext.GetRecentCaster(TimeSpan.FromSeconds(CombatContext.SpellDamageWindowSeconds));
        owner ??= target;

        if (string.IsNullOrEmpty(owner))
            return false;

        _combatContext.RegisterEffectOwnership(effectName, owner, target, DateTime.Now);
        Logger.Debug(LogCategory, $"Effet enregistré: {effectName} -> {owner} (cible: {target})");
        return true;
    }

    private static string NormalizeEffectName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var normalized = NormalizeEntityName(raw);
        normalized = normalized.Replace("%", string.Empty)
                               .Replace("!", string.Empty)
                               .Replace("?", string.Empty)
                               .Trim();

        normalized = NumericWordRegex.Replace(normalized, string.Empty).Trim();

        if (normalized.Length < 3)
            return string.Empty;

        if (IgnoredEffectNames.Contains(normalized))
            return string.Empty;

        if (char.IsDigit(normalized[0]))
            return string.Empty;

        return normalized;
    }

    private void RegisterUnaccountedLine(string line, string reason)
    {
        try
        {
            _combatContext.UnaccountedLines.Add(line);

            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {reason}: {line}{Environment.NewLine}";
            File.AppendAllText(UnaccountedLogPath, entry, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de l'écriture dans le log 'untracked': {ex.Message}");
        }
    }
    
    /// <summary>
    /// Réinitialise le parseur pour un nouveau combat
    /// </summary>
    public void Reset()
    {
        foreach (var stats in _playerStats.Values)
        {
            stats.DamageDealt = 0;
            stats.DamageTaken = 0;
            stats.HealingDone = 0;
            stats.ShieldGiven = 0;
            stats.DamageBySummon = 0;
            stats.DamageByAOE = 0;
            stats.NumberOfTurns = 0;
            stats.ResetTurnDamage();
        }

        _combatContext.Reset();
        _currentCombatPlayers.Clear();
        _playersPendingRemoval = new HashSet<string>(_playerStats.Keys, StringComparer.OrdinalIgnoreCase);
        _rosterFinalized = _playersPendingRemoval.Count == 0;
        _currentState = CombatState.Waiting;
    }

    private void EnsureRosterFinalized()
    {
        if (_rosterFinalized)
            return;

        if (_currentCombatPlayers.Count == 0)
            return;

        if (_playersPendingRemoval.Count == 0)
        {
            _rosterFinalized = true;
            return;
        }

        foreach (var player in _playersPendingRemoval.ToList())
        {
            if (_playerStats.Remove(player))
            {
                PlayerRemoved?.Invoke(this, player);
                Logger.Info(LogCategory, $"Joueur retiré du nouveau combat: {player}");
            }
        }

        _playersPendingRemoval.Clear();
        _rosterFinalized = true;
    }
}

