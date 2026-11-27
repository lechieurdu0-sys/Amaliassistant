using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOverlay.Kikimeter.Models;

public static class PlayerOrderMembership
{
    public static IReadOnlyList<string> Order(IEnumerable<string> live, params IEnumerable<string>[] preferences)
    {
        var remaining = new HashSet<string>(live.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var name in preferences.SelectMany(p => p).Concat(live))
            if (remaining.Remove(name)) result.Add(name);
        return result;
    }
}
