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
    private volatile CombatState _currentState;
    private PlayerStats[] _playerSnapshot = Array.Empty<PlayerStats>();
    private string[] _combatPlayerKeys = Array.Empty<string>();

    private static readonly string UnaccountedLogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "logs",
        "kikimeter_untracked.log");
    
    // Expressions régulières pour le parsing (support noms multi-mots)
    private static readonly Regex PlayerJoinRegex = new Regex(
        @".*?\[_FL_\].*?fightId=\d+\s+(.+?)\s+breed\s*:\s*(\d+)\s+\[(\d+)\]\s+isControlledByAI=(true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    // Observed in the French client: this is a turn boundary, not a named actor or round number.
    private static readonly Regex TurnCarryoverRegex = new(
        @"\[Information \(combat\)\]\s+\d+ secondes? reportées? pour le tour suivant\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CriticalSuffixRegex = new(
        @"\s+\(Critiques?\)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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
        @".*?\[Information \(combat\)\]\s+(.+?):\s+-([\d\s\u00A0\u202F]+)\s+PV\s*(?:\(([^)]+)\))?\s*(?:\(([^)]+)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    private static readonly Regex HealingRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+\+([\d\s\u00A0\u202F]+)\s+PV\s*(?:\(([^)]+)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    private static readonly Regex ShieldRegex = new Regex(
        @".*?\[Information \(combat\)\]\s+(.+?):\s+([\d\s\u00A0\u202F]+)\s+Armure",
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

    // Publish membership on the parser thread; readers never enumerate the mutable roster.
    public PlayerStats[] GetPlayerSnapshot() =>
        System.Threading.Volatile.Read(ref _playerSnapshot).ToArray();

    public HashSet<string> GetCombatPlayerKeys() =>
        new HashSet<string>(System.Threading.Volatile.Read(ref _combatPlayerKeys), StringComparer.OrdinalIgnoreCase);

    private void PublishRoster()
    {
        System.Threading.Volatile.Write(ref _playerSnapshot, _playerStats.Values.ToArray());
        var keys = _currentCombatPlayers
            .Where(name => _playerStats.ContainsKey(name))
            .SelectMany(name => new[] { name, _playerStats[name].PlayerId.ToString(CultureInfo.InvariantCulture) })
            .ToArray();
        System.Threading.Volatile.Write(ref _combatPlayerKeys, keys);
    }
    
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
            
            // Fallback robuste:
            // certains logs ne remontent pas toujours "CREATION DU COMBAT" au bon moment.
            // Si on voit des joueurs rejoindre un combat alors que l'état n'est pas actif,
            // on démarre le combat implicitement pour ne pas figer le Kikimeter.
            if (_currentState != CombatState.Active)
            {
                bool hasJoinMarker =
                    line.Contains("join the fight at", StringComparison.Ordinal) &&
                    line.Contains("isControlledByAI=false", StringComparison.Ordinal);

                bool hasCombatActionMarker =
                    line.Contains("[Information (combat)]", StringComparison.Ordinal) &&
                    (line.Contains("lance le sort", StringComparison.Ordinal) ||
                     line.Contains(" PV", StringComparison.Ordinal) ||
                     line.Contains("Armure", StringComparison.Ordinal));

                if (hasJoinMarker || hasCombatActionMarker)
                {
                    Logger.Info(LogCategory, $"Début de combat implicite détecté (join={hasJoinMarker}, action={hasCombatActionMarker}).");
                    HandleCombatStart();
                }
                else
                {
                    return;
                }
            }

            if (TurnCarryoverRegex.IsMatch(line))
            {
                // A single player can take consecutive turns without a monster casting a spell.
                // Retain effect ownership but do not reuse the preceding direct cast.
                _combatContext.PreviousCaster = null;
                _combatContext.CurrentCaster = null;
                _combatContext.CurrentSpell = null;
                _combatContext.PendingSummonOwner = null;
                _combatContext.PendingSummonName = null;
                _combatContext.RecentSpells.Clear();
                return;
            }

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
        EnsureRosterFinalized();
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

        bool firstJoin = _currentCombatPlayers.Add(playerName);
        stats.IsActive = true;
        stats.LastSeenInCombat = DateTime.Now;
        _playersPendingRemoval.Remove(playerName);
        PublishRoster();
        if (firstJoin)
            PlayerAdded?.Invoke(this, stats);

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
        
        _combatContext.RegisterSummonName(summonName, summonId);
        if (_combatContext.PendingSummonOwner != null &&
            string.Equals(_combatContext.PendingSummonName, summonName, StringComparison.OrdinalIgnoreCase))
        {
            _combatContext.SummonOwnership.TryAdd(summonId, _combatContext.PendingSummonOwner);
            _combatContext.PendingSummonOwner = null;
            _combatContext.PendingSummonName = null;
        }
    }

    private void ParseSpellCast(string line)
    {
        EnsureRosterFinalized();

        var match = SpellCastRegex.Match(line);
        if (!match.Success)
            return;
        
        string caster = NormalizeEntityName(match.Groups[1].Value);
        string spell = SpellMetadataProvider.NormalizeSpellName(CriticalSuffixRegex.Replace(match.Groups[2].Value.Trim(), string.Empty));

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
        
        _combatContext.PendingSummonOwner = _combatContext.IsSummonSpell ? ResolvePlayerOwner(caster) : null;
        _combatContext.PendingSummonName = null;
        
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
            }
            else if (_combatContext.PreviousCaster == null || _combatContext.PreviousCaster == string.Empty)
            {
                // Premier tour de ce joueur dans ce combat
                _playerStats[caster].NumberOfTurns++;
                _playerStats[caster].ResetTurnDamage(); // Initialiser à 0 pour le premier tour
                Logger.Info(LogCategory, $"Premier tour pour {caster}");
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
            _combatContext.PendingSummonOwner = ResolvePlayerOwner(caster);
            _combatContext.PendingSummonName = summonName;
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
        
        var owner = _combatContext.PendingSummonOwner;
        
        if (owner != null)
        {
            _combatContext.SummonOwnership.TryAdd(summonId, owner);
            _combatContext.PendingSummonOwner = null;
            _combatContext.PendingSummonName = null;
            Logger.Debug(LogCategory, $"Association invocation: id={summonId} -> {owner}");
        }
    }
    
    private void ParseCombatEffect(string line)
    {
        if (line.Contains("PV", StringComparison.Ordinal))
        {
            if (DamageRegex.IsMatch(line))
                ParseDamage(line);
            else if (HealingRegex.IsMatch(line))
                ParseHealing(line);
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
        long damage = ParseCombatAmount(match.Groups[2].Value);
        string? effect = NormalizeEffectName(match.Groups[4].Value);
        if (string.IsNullOrEmpty(effect))
        {
            var first = NormalizeEffectName(match.Groups[3].Value);
            if (IsKnownIndirectEffect(first))
                effect = first;
        }

        if (_currentCombatPlayers.Contains(target) && _playerStats.TryGetValue(target, out var recipient))
            recipient.DamageTaken += damage;

        var owner = ResolveActionOwner(effect, target);
        if (owner != null)
        {
            var stats = _playerStats[owner];
            stats.DamageDealt += damage;
            stats.DamageThisTurn += damage;
            if (!string.Equals(_combatContext.CurrentCaster, owner, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ResolvePlayerOwner(_combatContext.CurrentCaster), owner, StringComparison.OrdinalIgnoreCase))
                stats.DamageBySummon += damage;
        }
        else if (_combatContext.CurrentCaster == null ||
                 _combatContext.SummonNameToId.ContainsKey(_combatContext.CurrentCaster) ||
                 IsKnownIndirectEffect(effect))
            RegisterUnaccountedLine(line, "Damage-OwnerUnknown");
        // Monsters are not tracked as players. Their damage to players was counted above.
    }

    private void ParseHealing(string line)
    {
        EnsureRosterFinalized();
        var match = HealingRegex.Match(line);
        if (!match.Success) return;
        string target = NormalizeEntityName(match.Groups[1].Value);
        var owner = ResolveActionOwner(NormalizeEffectName(match.Groups[3].Value), target);
        if (_currentCombatPlayers.Contains(target) && owner != null)
            _playerStats[owner].HealingDone += ParseCombatAmount(match.Groups[2].Value);
    }

    private void ParseShield(string line)
    {
        EnsureRosterFinalized();
        var match = ShieldRegex.Match(line);
        if (!match.Success) return;
        string target = NormalizeEntityName(match.Groups[1].Value);
        var owner = ResolvePlayerOwner(_combatContext.CurrentCaster);
        if (_currentCombatPlayers.Contains(target) && owner != null)
            _playerStats[owner].ShieldGiven += ParseCombatAmount(match.Groups[2].Value);
    }

    private static bool IsKnownIndirectEffect(string? effect) =>
        !string.IsNullOrEmpty(effect) &&
        (SpellMetadataProvider.IsIndirectEffect(effect) ||
         string.Equals(effect, "Courroux", StringComparison.OrdinalIgnoreCase));

    private string? ResolveActionOwner(string? effect, string target)
    {
        if (!string.IsNullOrEmpty(effect))
        {
            var registered = _combatContext.TryResolveEffectOwner(effect, target, DateTime.Now);
            if (registered != null)
                return ResolvePlayerOwner(registered);
            if (IsKnownIndirectEffect(effect))
                return null;
        }
        return ResolvePlayerOwner(_combatContext.CurrentCaster);
    }

    private string? ResolvePlayerOwner(string? entity)
    {
        if (string.IsNullOrEmpty(entity))
            return null;
        if (_currentCombatPlayers.Contains(entity) && _playerStats.ContainsKey(entity))
            return entity;
        var owner = _combatContext.ResolveSummonOwner(entity);
        return owner != null && _currentCombatPlayers.Contains(owner) && _playerStats.ContainsKey(owner)
            ? owner : null;
    }

    private static long ParseCombatAmount(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return 0;

        // Les logs Wakfu utilisent parfois des séparateurs de milliers unicode (ex: 1 003).
        var normalized = SpaceBetweenDigitsRegex.Replace(rawValue, string.Empty);
        normalized = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);

        return long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
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

        var owner = ResolvePlayerOwner(_combatContext.CurrentCaster);

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
        PublishRoster();
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
                PublishRoster();
                PlayerRemoved?.Invoke(this, player);
                Logger.Info(LogCategory, $"Joueur retiré du nouveau combat: {player}");
            }
        }

        _playersPendingRemoval.Clear();
        _rosterFinalized = true;
    }
}

