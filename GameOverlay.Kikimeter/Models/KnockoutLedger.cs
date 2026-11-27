using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameOverlay.Kikimeter.Models;

// Fight-local evidence, not a simulation of the KO countdown.
public sealed class KnockoutLedger
{
    public sealed record Entry(string Name, long KoSequence, TimeSpan? KoTime,
        int KoRound, int HitCount, long LastHitSequence, TimeSpan? LastHitTime,
        long LastHitCast, long LastDamage, bool InferredDefeat);

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex Clock = new(@"(?:^|\s)(\d{2}:\d{2}:\d{2},\d{3})(?:\s|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private long _sequence;
    private long _cast;
    private long _lastDefeatCast = -1;
    private long _lastDefeatSequence = -1;
    private TimeSpan? _clock;
    public IReadOnlyList<Entry> Snapshot => _entries.Values.OrderBy(x => x.KoSequence).ToArray();

    public void ObserveLine(string line)
    {
        _sequence++;
        var match = Clock.Match(line);
        _clock = match.Success && TimeSpan.TryParseExact(match.Groups[1].Value,
            @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out var time) ? time : null;
    }

    public void ObserveCast() => _cast++;

    public void KnockOut(string name, int round)
    {
        if (_entries.TryGetValue(name, out var previous) && !previous.InferredDefeat) return;
        _entries[name] = new Entry(name, _sequence, _clock, round, 0, 0, null, -1, 0, false);
    }

    public void ObserveDamage(string name, long amount)
    {
        if (amount <= 0 || !_entries.TryGetValue(name, out var entry) || entry.InferredDefeat)
            return;
        _entries[name] = entry with { HitCount = entry.HitCount + 1, LastHitSequence = _sequence,
            LastHitTime = _clock, LastHitCast = _cast, LastDamage = amount };
    }

    public string? InferDefeat()
    {
        // Never apply repeated unnamed defeat messages within the same cast/evidence.
        if (_lastDefeatCast == _cast &&
            !_entries.Values.Any(e => !e.InferredDefeat &&
                (e.KoSequence > _lastDefeatSequence || e.LastHitSequence > _lastDefeatSequence)))
            return null;
        _lastDefeatCast = _cast;
        _lastDefeatSequence = _sequence;

        var candidates = _entries.Values.Where(e => !e.InferredDefeat).ToArray();
        Entry? chosen = null;
        if (candidates.Length == 1)
            chosen = candidates[0];
        else if (candidates.Length > 1)
        {
            // A named -1 PV impact during KO immediately before defeat is supporting
            // evidence, not proof. Never choose between multiple matching targets.
            var hits = candidates.Where(e => e.LastDamage == 1 && e.LastHitCast == _cast &&
                e.LastHitSequence > e.KoSequence && IsRecent(e.LastHitTime, _clock)).ToArray();
            if (hits.Length == 1) chosen = hits[0];
        }
        if (chosen == null) return null;
        _entries[chosen.Name] = chosen with { InferredDefeat = true };
        return chosen.Name;
    }

    private static bool IsRecent(TimeSpan? hit, TimeSpan? now)
    {
        if (hit == null || now == null) return false;
        var elapsed = now.Value - hit.Value;
        if (elapsed < TimeSpan.Zero) elapsed += TimeSpan.FromDays(1);
        return elapsed <= TimeSpan.FromSeconds(5);
    }

    // A named revival supersedes an inferred defeat. Duplicate revival messages
    // are harmless because the active KO record is removed exactly once.
    public bool Revive(string name) => _entries.Remove(name);

    public void Reset()
    {
        _entries.Clear();
        _sequence = _cast = 0;
        _lastDefeatCast = _lastDefeatSequence = -1;
        _clock = null;
    }
}
