using System;
using System.IO;
using GameOverlay.Models;

internal static class TestBuildModeRegression
{
    public static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Amaliassistant-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            if (TestBuildMode.IsTestFolder(folder)) throw new InvalidOperationException("Normal build classified as test");
            File.WriteAllText(Path.Combine(folder, "AMALIASSISTANT_TEST"), "test");
            if (!TestBuildMode.IsTestFolder(folder)) throw new InvalidOperationException("Test marker ignored");
        }
        finally { Directory.Delete(folder, true); }
    }
}
