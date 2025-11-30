using System;
using System.IO;
using Newtonsoft.Json;

namespace GameOverlay.Models
{
    public static class PersistentStorageHelper
    {
        private const string AppDataFolderName = "Amaliassistant";

        private static string EnsureAppDataDirectory()
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }

        public static string GetAppDataPath(string fileName)
        {
            return Path.Combine(EnsureAppDataDirectory(), fileName);
        }

        public static string GetLegacyPath(string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        public static T LoadJsonWithFallback<T>(string fileName) where T : new()
        {
            var primaryPath = GetAppDataPath(fileName);
            if (File.Exists(primaryPath))
            {
                try
                {
                    var json = File.ReadAllText(primaryPath);
                    return JsonConvert.DeserializeObject<T>(json) ?? new T();
                }
                catch
                {
                    // ignore le fichier corrompu et tente la sauvegarde legacy
                }
            }

            var legacyPath = GetLegacyPath(fileName);
            if (File.Exists(legacyPath))
            {
                try
                {
                    var json = File.ReadAllText(legacyPath);
                    var data = JsonConvert.DeserializeObject<T>(json) ?? new T();
                    try
                    {
                        File.WriteAllText(primaryPath, json);
                    }
                    catch
                    {
                        // Ignorer les erreurs de migration, l'appli utilisera quand même les données chargées
                    }

                    return data;
                }
                catch
                {
                    // Fichier legacy illisible, on repart sur un état vierge
                }
            }

            return new T();
        }

        public static void SaveJson(string fileName, object data, bool duplicateToLegacy = true)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var primaryPath = GetAppDataPath(fileName);
            File.WriteAllText(primaryPath, json);

            if (!duplicateToLegacy)
            {
                return;
            }

            var legacyPath = GetLegacyPath(fileName);
            try
            {
                File.WriteAllText(legacyPath, json);
            }
            catch
            {
                // Certaines installations n'autorisent pas l'écriture dans le dossier legacy, ce n'est pas bloquant.
            }
        }
    }
}


