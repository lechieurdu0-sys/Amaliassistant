using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameOverlay.Kikimeter;

namespace GameOverlay.Kikimeter.Core;

/// <summary>
/// Fournit des métadonnées sur les sorts (classe d'origine, coût PA, alias...)
/// basées sur la base de données JSON importée depuis WakMeter.
/// </summary>
public static class SpellMetadataProvider
{
    private record SpellMetadata(string ClassName, int Cost);

    private static readonly Lazy<Dictionary<string, SpellMetadata>> SpellLookup = new(LoadSpellMetadata);

    private static readonly Dictionary<string, string> SpellAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Mapping importé de WakMeter (SpecialCase.java)
        ["Exaltation"] = "Cataclysme"
    };

    private static readonly HashSet<string> IndirectEffectNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Liste importée de IndirectAbilities.java (WakMeter)
        "Enflammé",
        "Contre-attaque",
        "Marque itsade"
    };

    public static string NormalizeSpellName(string spellName)
    {
        if (string.IsNullOrWhiteSpace(spellName))
            return string.Empty;

        return SpellAliases.TryGetValue(spellName.Trim(), out var mapped)
            ? mapped
            : spellName.Trim();
    }

    public static string? GetClassForSpell(string spellName)
    {
        var metadata = TryGetMetadata(spellName);
        return metadata?.ClassName;
    }

    public static int? GetCostForSpell(string spellName)
    {
        var metadata = TryGetMetadata(spellName);
        return metadata?.Cost;
    }

    public static bool IsIndirectEffect(string effectName)
    {
        if (string.IsNullOrWhiteSpace(effectName))
            return false;

        return IndirectEffectNames.Contains(effectName.Trim());
    }

    private static SpellMetadata? TryGetMetadata(string spellName)
    {
        if (string.IsNullOrWhiteSpace(spellName))
            return null;

        var lookup = SpellLookup.Value;
        lookup.TryGetValue(spellName.Trim(), out var metadata);
        return metadata;
    }

    private static Dictionary<string, SpellMetadata> LoadSpellMetadata()
    {
        var result = new Dictionary<string, SpellMetadata>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var jsonPath = Path.Combine(baseDirectory, "Resources", "SortsPA.json");

            if (!File.Exists(jsonPath))
                return result;

            using var stream = File.OpenRead(jsonPath);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var arrayItem in document.RootElement.EnumerateArray())
            {
                if (arrayItem.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var classProperty in arrayItem.EnumerateObject())
                {
                    var className = classProperty.Name;
                    if (classProperty.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    foreach (var spellProperty in classProperty.Value.EnumerateObject())
                    {
                        if (spellProperty.Value.ValueKind == JsonValueKind.Number && spellProperty.Value.TryGetInt32(out var cost))
                        {
                            result[spellProperty.Name.Trim()] = new SpellMetadata(className.Trim(), cost);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SpellMetadataProvider", $"Impossible de charger SortsPA.json: {ex.Message}");
        }

        return result;
    }
}

