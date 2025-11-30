using System;
using System.Collections.Generic;

namespace GameOverlay.XpTracker.Models;

public sealed class XpWindowState
{
    public string EntityName { get; set; } = string.Empty;
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 80;
    public string ProgressColor { get; set; } = "#FF00AEEF";
    public bool IsVisible { get; set; }
}

public sealed class XpWindowStateCollection
{
    public Dictionary<string, XpWindowState> Windows { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}



