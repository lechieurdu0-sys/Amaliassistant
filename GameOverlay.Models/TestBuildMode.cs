using System;
using System.IO;

namespace GameOverlay.Models;

// The CI test archive carries this marker. Production installations are unchanged.
public static class TestBuildMode
{
    public static bool IsEnabled => IsTestFolder(AppContext.BaseDirectory);
    public static bool IsTestFolder(string folder) =>
        File.Exists(Path.Combine(folder, "AMALIASSISTANT_TEST"));
}
