using System;
using System.IO;
using System.Text;
using GameOverlay.Kikimeter.Services.Catalog;

internal static class WakfuCatalogRegression
{
    public static void Run()
    {
        const string json = """
        {"classes":[{"id":20,"name":"NewClass"},{"id":15,"name":"OtherClass"}],
         "spells":[{"id":1,"breedId":20,"name":"NewSpell","pa":3,"passive":0},
                   {"id":2,"breedId":15,"name":"Shared","pa":2,"passive":0},
                   {"id":3,"breedId":20,"name":"Shared","pa":4,"passive":0},
                   {"id":4,"breedId":15,"name":"NewSpell","pa":0,"passive":2},
                   {"id":5,"breedId":15,"name":"SameCost","pa":1,"passive":0},
                   {"id":6,"breedId":20,"name":"SameCost","pa":1,"passive":0}]}
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var catalog = WakfuCatalog.Read(stream);
        if (catalog.ClassForBreed(20) != "NewClass" || catalog.ClassForBreed(17) != null ||
            catalog.ClassForSpell(" newspell ") != "NewClass" || catalog.CostForSpell("NewSpell") != 3 ||
            catalog.ClassForSpell("Shared") != null || catalog.CostForSpell("Shared") != null ||
            catalog.ClassForSpell("SameCost") != null || catalog.CostForSpell("SameCost") != 1 ||
            catalog.ClassForSpell("Unknown") != null || catalog.CostForSpell("Unknown") != null)
            throw new InvalidOperationException("Catalog authority / ambiguity regression");
    }
}
