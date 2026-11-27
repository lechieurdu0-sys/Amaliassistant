using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameOverlay.Kikimeter.Services.Catalog;

public enum CatalogValidationStatus
{
    Valid, Missing, InvalidManifest, Incompatible, ChecksumMismatch, Unavailable
}

public sealed record CatalogManifest(int ContractVersion, string GameVersion,
    string ExporterVersion, string DatabaseSha256);

public sealed record CatalogValidationResult(CatalogValidationStatus Status,
    CatalogManifest? Manifest = null);

// Optional local catalog preflight only. Does not open SQLite or authorize SQL.
// Run off the UI thread; callers must revalidate if the database changes.
public static class CatalogManifestValidator
{
    private const int MaxManifestBytes = 16 * 1024;

    public static CatalogValidationResult Validate(string manifestPath, string databasePath,
        string expectedGameVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedGameVersion);
        try
        {
            using var manifestStream = File.OpenRead(manifestPath);
            var bytes = new byte[MaxManifestBytes + 1];
            int length = manifestStream.ReadAtLeast(bytes, bytes.Length, throwIfEndOfStream: false);
            if (length > MaxManifestBytes)
                return new(CatalogValidationStatus.InvalidManifest);

            using var document = JsonDocument.Parse(bytes.AsMemory(0, length));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(CatalogValidationStatus.InvalidManifest);

            // Reject duplicate names, including unknown extension fields.
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!names.Add(property.Name))
                    return new(CatalogValidationStatus.InvalidManifest);

            if (!root.TryGetProperty("contractVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out var contractVersion) ||
                !TryString(root, "gameVersion", out var gameVersion) ||
                !TryString(root, "exporterVersion", out var exporterVersion) ||
                !TryString(root, "databaseSha256", out var sha) ||
                sha.Length != 64 || !IsHex(sha))
                return new(CatalogValidationStatus.InvalidManifest);

            if (contractVersion != 1 ||
                !string.Equals(gameVersion, expectedGameVersion, StringComparison.Ordinal))
                return new(CatalogValidationStatus.Incompatible);

            // Stream the hash; never load the database into memory.
            using var databaseStream = File.OpenRead(databasePath);
            var actual = Convert.ToHexString(SHA256.HashData(databaseStream));
            if (!string.Equals(sha, actual, StringComparison.OrdinalIgnoreCase))
                return new(CatalogValidationStatus.ChecksumMismatch);

            return new(CatalogValidationStatus.Valid,
                new CatalogManifest(contractVersion, gameVersion, exporterVersion, actual));
        }
        catch (JsonException) { return new(CatalogValidationStatus.InvalidManifest); }
        catch (FileNotFoundException) { return new(CatalogValidationStatus.Missing); }
        catch (DirectoryNotFoundException) { return new(CatalogValidationStatus.Missing); }
        catch (IOException) { return new(CatalogValidationStatus.Unavailable); }
        catch (UnauthorizedAccessException) { return new(CatalogValidationStatus.Unavailable); }
    }

    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsHex(string value)
    {
        foreach (char c in value)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
