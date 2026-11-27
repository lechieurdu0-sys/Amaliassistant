using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOverlay.Kikimeter.Models;

public enum RoundParticipantState { Available, KnockedOut, Dead }

/// <summary>
/// Estimated global round from observed player passages. Monsters provide separation
/// evidence, but do not enter the player list. Summons share their master's passage.
/// This deliberately does not infer initiative from the order of roster joins.
/// </summary>
public sealed class CombatRoundTracker
{
    private readonly List<string> _order = new();
    private readonly Dictionary<string, RoundParticipantState> _states =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenThisRound = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastOwner;
    private bool _interveningActor;

    public int Round { get; private set; }
    public string? Anchor => _order.FirstOrDefault(IsAvailable);
    public IReadOnlyList<string> ObservedOrder => _order.ToArray();

    public RoundParticipantState? GetState(string name) =>
        _states.TryGetValue(name, out var state) ? state : null;

    private bool IsAvailable(string name) => GetState(name) == RoundParticipantState.Available;

    public void RegisterPlayer(string name)
    {
        _states.TryAdd(name, RoundParticipantState.Available);
    }

    public bool SetState(string name, RoundParticipantState state)
    {
        if (!_states.TryGetValue(name, out var previous) || previous == RoundParticipantState.Dead ||
            previous == state)
            return false;
        _states[name] = state;
        if (state == RoundParticipantState.Dead)
            _order.RemoveAll(p => StringComparer.OrdinalIgnoreCase.Equals(p, name));
        // KO retains the initiative slot and previous round participation.
        if (state != RoundParticipantState.Available &&
            StringComparer.OrdinalIgnoreCase.Equals(_lastOwner, name))
            _interveningActor = true;
        return true;
    }

    public void ObserveMonsterAction()
    {
        if (Round > 0)
            _interveningActor = true;
    }

    public void ObservePlayerPassage(string owner)
    {
        if (!IsAvailable(owner)) return;
        if (!_order.Contains(owner, StringComparer.OrdinalIgnoreCase))
            _order.Add(owner);
        if (Round == 0)
            Round = 1;

        bool separated = _interveningActor ||
            (_lastOwner != null && !StringComparer.OrdinalIgnoreCase.Equals(_lastOwner, owner));
        // Return to the first available observed player, after another actor has acted.
        // If the old anchor died before the new anchor played, it is still the same round.
        if (StringComparer.OrdinalIgnoreCase.Equals(Anchor, owner) &&
            _seenThisRound.Contains(owner) && separated)
        {
            Round++;
            _seenThisRound.Clear();
        }

        _seenThisRound.Add(owner);
        _lastOwner = owner;
        _interveningActor = false;
    }

    public void Reset()
    {
        Round = 0;
        _order.Clear();
        _states.Clear();
        _seenThisRound.Clear();
        _lastOwner = null;
        _interveningActor = false;
    }
}
