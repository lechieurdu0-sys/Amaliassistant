using System;
using GameOverlay.Models;

internal static class OverlayCanvasRegression
{
    public static void Run()
    {
        Check(OverlayCanvasBounds.Clamp(2400, 300, 3840, 1080, 70, 172), 2400, 300);
        Check(OverlayCanvasBounds.Restore(2400, 300, 960, 540, 3840, 1080, 70, 172), 2400, 300);
        // Primary screen is offset 1920 units to the right of the virtual desktop origin.
        Check(OverlayCanvasBounds.Restore(-1, -1, 2880, 540, 3840, 1080, 70, 172), 2880, 540);
        Check(OverlayCanvasBounds.Clamp(300, 1600, 1920, 2160, 70, 172), 300, 1600);
        Check(OverlayCanvasBounds.Restore(2400, 300, 960, 540, 1920, 1080, 70, 172), 960, 540);
        Check(OverlayCanvasBounds.Clamp(3900, 1100, 3840, 1080, 70, 172), 3770, 908);
        Check(OverlayCanvasBounds.Clamp(-20, -40, 3840, 1080, 70, 172), 0, 0);
        Check(OverlayCanvasBounds.Clamp(20, 20, 40, 40, 70, 172), 0, 0);
    }
    private static void Check((double X, double Y) actual, double x, double y)
    {
        if (actual.X != x || actual.Y != y)
            throw new InvalidOperationException($"Expected ({x}, {y}), got {actual}");
    }
}
