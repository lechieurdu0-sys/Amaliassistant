using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using GameOverlay.Kikimeter.Services.Catalog;

internal static class CatalogManifestRegression
{
    public static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "amali-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string manifest = Path.Combine(directory, "manifest.json");
            string database = Path.Combine(directory, "synthetic.bin");
            byte[] fixture = { 1, 2, 3, 4, 5 };
            string sha = Convert.ToHexString(SHA256.HashData(fixture)).ToLowerInvariant();
            string Json(int version = 1, string game = "1.94", string exporter = "synthetic", string? hash = null)
                => JsonSerializer.Serialize(new { contractVersion = version, gameVersion = game,
                    exporterVersion = exporter, databaseSha256 = hash ?? sha });

            void Expect(CatalogValidationStatus expected, string scenario, string? json)
            {
                if (json is null) File.Delete(manifest);
                else File.WriteAllText(manifest, json);
                var result = CatalogManifestValidator.Validate(manifest, database, "1.94");
                if (result.Status != expected)
                    throw new InvalidOperationException(scenario + ": expected " + expected + ", got " + result.Status);
                if ((result.Manifest is not null) != (expected == CatalogValidationStatus.Valid))
                    throw new InvalidOperationException(scenario + ": unexpected manifest payload");
                if (result.Manifest is { } info &&
                    (info.ContractVersion != 1 || info.GameVersion != "1.94" ||
                     info.ExporterVersion != "synthetic" || info.DatabaseSha256 != sha.ToUpperInvariant()))
                    throw new InvalidOperationException(scenario + ": incorrect metadata");
            }

            File.WriteAllBytes(database, fixture);
            Expect(CatalogValidationStatus.Missing, "Missing manifest", null);
            Expect(CatalogValidationStatus.InvalidManifest, "Malformed JSON", "{");
            Expect(CatalogValidationStatus.InvalidManifest, "Wrong root", "[]");
            Expect(CatalogValidationStatus.InvalidManifest, "Missing fields", "{}");
            Expect(CatalogValidationStatus.InvalidManifest, "Wrong field type", Json().Replace("\"contractVersion\":1", "\"contractVersion\":\"1\""));
            Expect(CatalogValidationStatus.InvalidManifest, "Duplicate property", Json().Insert(1, "\"contractVersion\":1,"));
            Expect(CatalogValidationStatus.InvalidManifest, "Oversized manifest", new string(' ', 16 * 1024 + 1));
            Expect(CatalogValidationStatus.InvalidManifest, "Empty exporter", Json(exporter: " "));
            Expect(CatalogValidationStatus.InvalidManifest, "Invalid hash length", Json(hash: "ab"));
            Expect(CatalogValidationStatus.InvalidManifest, "Invalid hex", Json(hash: new string('z', 64)));
            Expect(CatalogValidationStatus.Incompatible, "Unsupported contract", Json(version: 2));
            Expect(CatalogValidationStatus.Incompatible, "Wrong game version", Json(game: "1.93"));
            Expect(CatalogValidationStatus.ChecksumMismatch, "Wrong checksum", Json(hash: new string('0', 64)));
            Expect(CatalogValidationStatus.Valid, "Valid manifest", Json());
            if (!File.ReadAllBytes(database).AsSpan().SequenceEqual(fixture))
                throw new InvalidOperationException("Validation modified the database");
            File.WriteAllBytes(database, new byte[] { 6 });
            Expect(CatalogValidationStatus.ChecksumMismatch, "Modified database", Json());
            File.Delete(database);
            Expect(CatalogValidationStatus.Missing, "Missing database", Json());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
