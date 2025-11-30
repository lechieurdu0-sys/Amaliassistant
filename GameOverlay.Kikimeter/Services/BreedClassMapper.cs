using System.Collections.Generic;

namespace GameOverlay.Kikimeter.Services;

internal static class BreedClassMapper
{
    private static readonly Dictionary<int, string> BreedToClass = new()
    {
        [1] = "Féca",
        [2] = "Osamodas",
        [3] = "Enutrof",
        [4] = "Sram",
        [5] = "Xélor",
        [6] = "Écaflip",
        [7] = "Eniripsa",
        [8] = "Iop",
        [9] = "Cra",
        [10] = "Sadida",
        [11] = "Sacrieur",
        [12] = "Pandawa",
        [13] = "Roublard",
        [14] = "Zobal",
        [15] = "Steamer",
        [16] = "Eliotrope",
        [17] = "Huppermage",
        [18] = "Ouginak"
    };

    public static string? GetClassName(string? breedValue)
    {
        if (string.IsNullOrWhiteSpace(breedValue))
            return null;

        if (!int.TryParse(breedValue.Trim(), out var breedId))
            return null;

        return BreedToClass.TryGetValue(breedId, out var className) ? className : null;
    }

    public static string? GetClassName(int breedId)
    {
        return BreedToClass.TryGetValue(breedId, out var className) ? className : null;
    }
}










