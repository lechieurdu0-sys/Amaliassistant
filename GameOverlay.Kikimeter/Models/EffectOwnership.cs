using System;

namespace GameOverlay.Kikimeter.Models;

public class EffectOwnership
{
    public EffectOwnership(string effectName, string owner, string? target, DateTime registeredAt, TimeSpan lifetime)
    {
        EffectName = effectName;
        Owner = owner;
        Target = target;
        RegisteredAt = registeredAt;
        Lifetime = lifetime;
        LastSeenAt = registeredAt;
    }

    public string EffectName { get; }

    public string Owner { get; }

    public string? Target { get; }

    public DateTime RegisteredAt { get; }

    public TimeSpan Lifetime { get; }

    public DateTime LastSeenAt { get; private set; }

    public bool IsExpired(DateTime timestamp) => timestamp - LastSeenAt > Lifetime;

    public void Refresh(DateTime timestamp)
    {
        LastSeenAt = timestamp;
    }
}





