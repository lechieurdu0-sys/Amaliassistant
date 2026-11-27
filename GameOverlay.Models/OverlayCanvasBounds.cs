using System;

namespace GameOverlay.Models;

/// <summary>Geometry in Canvas-local WPF units, independent of physical screen coordinates.</summary>
public static class OverlayCanvasBounds
{
    public static (double X, double Y) Clamp(double x, double y, double width, double height,
        double itemWidth, double itemHeight)
    {
        return (ClampAxis(x, width, itemWidth), ClampAxis(y, height, itemHeight));
    }

    public static (double X, double Y) Restore(double x, double y, double defaultX, double defaultY,
        double width, double height, double itemWidth, double itemHeight)
    {
        // A negative coordinate is invalid in Canvas space, including the historical -1 sentinel.
        // Recover both axes together, so an unplugged monitor does not produce a mixed position.
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 ||
            x >= width || y >= height)
            return Clamp(defaultX, defaultY, width, height, itemWidth, itemHeight);
        return Clamp(x, y, width, height, itemWidth, itemHeight);
    }

    private static double ClampAxis(double position, double extent, double itemSize)
    {
        if (!double.IsFinite(position) || !double.IsFinite(extent) || !double.IsFinite(itemSize))
            return 0;
        return Math.Clamp(position, 0, Math.Max(0, extent - Math.Max(0, itemSize)));
    }
}
