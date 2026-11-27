using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameOverlay.Kikimeter;

namespace GameOverlay.Kikimeter.Services.Catalog;

// A small local projection of the master SQLite database, generated offline.
// No game data is embedded in the assembly or fetched over the network.
public sealed class WakfuCatalog
{
    public sealed record ClassRow(int Id, string Name);
    public sealed record SpellRow(int Id, int BreedId, string Name, int Pa, int Passive);
    public sealed record Snapshot(ClassRow[] Classes, SpellRow[] Spells);
    private readonly Dictionary<int, string> classes;
    private readonly Dictionary<string, SpellRow[]> spells;

    private WakfuCatalog(Snapshot snapshot)
    {
        classes = snapshot.Classes.ToDictionary(x => x.Id, x => x.Name);
        if (classes.Count == 0 || snapshot.Spells.Length == 0 ||
            snapshot.Classes.Any(x => x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name)) ||
            snapshot.Spells.Any(x => x.Id <= 0 || !classes.ContainsKey(x.BreedId) ||
                string.IsNullOrWhiteSpace(x.Name) || x.Pa < 0) ||
            snapshot.Spells.Select(x => x.Id).Distinct().Count() != snapshot.Spells.Length)
            throw new InvalidDataException("Invalid catalog rows");
        spells = snapshot.Spells.Where(x => x.Passive == 0)
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public static WakfuCatalog Read(Stream stream)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(stream,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
        if (snapshot?.Classes is null || snapshot.Spells is null)
            throw new InvalidDataException("Missing catalog collections");
        return new WakfuCatalog(snapshot);
    }

    public string? ClassForBreed(int id) => classes.TryGetValue(id, out var name) ? name : null;

    private SpellRow[] Find(string name) => spells.TryGetValue(name.Trim(), out var rows)
        ? rows : Array.Empty<SpellRow>();

    public string? ClassForSpell(string name)
    {
        var ids = Find(name).Select(x => x.BreedId).Distinct().ToArray();
        return ids.Length == 1 ? ClassForBreed(ids[0]) : null;
    }

    public int? CostForSpell(string name)
    {
        var costs = Find(name).Select(x => x.Pa).Distinct().ToArray();
        return costs.Length == 1 ? costs[0] : null;
    }
}

internal static class LocalWakfuCatalog
{
    private sealed record State(bool Present, WakfuCatalog? Catalog);
    private static readonly Lazy<State> Current = new(Load);
    public static bool Present => Current.Value.Present;
    public static WakfuCatalog? Catalog => Current.Value.Catalog;

    private static State Load()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Resources", "WakfuCatalog");
        string manifest = Path.Combine(root, "manifest.json");
        string projection = Path.Combine(root, "catalog.json");
        if (!Directory.Exists(root))
        {
            Logger.Warning(nameof(LocalWakfuCatalog), "Catalogue 1.94 absent; métadonnées historiques utilisées.");
            return new(false, null);
        }

        try
        {
            var result = CatalogManifestValidator.Validate(manifest, projection, "1.94");
            if (result.Status != CatalogValidationStatus.Valid)
            {
                Logger.Warning(nameof(LocalWakfuCatalog), "Catalogue local rejeté: " + result.Status);
                return new(true, null);
            }
            using var stream = File.OpenRead(projection);
            var catalog = WakfuCatalog.Read(stream);
            Logger.Info(nameof(LocalWakfuCatalog), "Catalogue Wakfu 1.94 local chargé.");
            return new(true, catalog);
        }
        catch (Exception ex)
        {
            Logger.Warning(nameof(LocalWakfuCatalog), "Catalogue local indisponible: " + ex.GetType().Name);
            return new(true, null);
        }
    }
}
