using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using GameOverlay.Models;
using Newtonsoft.Json;

namespace GameOverlay.App.Services
{
    /// <summary>
    /// Service pour récupérer les informations sur les plugins disponibles depuis le dépôt GitHub
    /// </summary>
    public class PluginRepositoryService
    {
        private const string GitHubRepo = "lechieurdu0-sys/Amaliassistant";
        private const string PluginsJsonUrl = $"https://raw.githubusercontent.com/{GitHubRepo}/main/plugins.json";
        private static readonly HttpClient _httpClient = new HttpClient();
        
        static PluginRepositoryService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Amaliassistant-PluginRepository/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }
        
        /// <summary>
        /// Récupère la liste des plugins disponibles depuis le dépôt GitHub
        /// </summary>
        public static async Task<List<PluginRepositoryInfo>> GetAvailablePluginsAsync()
        {
            try
            {
                Logger.Info("PluginRepositoryService", $"Récupération des plugins disponibles depuis: {PluginsJsonUrl}");
                
                var response = await _httpClient.GetStringAsync(PluginsJsonUrl);
                
                if (string.IsNullOrWhiteSpace(response))
                {
                    Logger.Info("PluginRepositoryService", "Réponse vide depuis le dépôt");
                    return new List<PluginRepositoryInfo>();
                }
                
                var plugins = JsonConvert.DeserializeObject<List<PluginRepositoryInfo>>(response);
                
                if (plugins == null)
                {
                    Logger.Info("PluginRepositoryService", "Impossible de désérialiser la liste des plugins");
                    return new List<PluginRepositoryInfo>();
                }
                
                Logger.Info("PluginRepositoryService", $"{plugins.Count} plugin(s) disponible(s) récupéré(s)");
                return plugins;
            }
            catch (Exception ex)
            {
                Logger.Error("PluginRepositoryService", $"Erreur lors de la récupération des plugins disponibles: {ex.Message}");
                return new List<PluginRepositoryInfo>();
            }
        }
        
        /// <summary>
        /// Télécharge un plugin depuis son URL
        /// </summary>
        public static async Task<bool> DownloadPluginAsync(string downloadUrl, string destinationPath)
        {
            try
            {
                Logger.Info("PluginRepositoryService", $"Téléchargement du plugin depuis: {downloadUrl}");
                
                var response = await _httpClient.GetByteArrayAsync(downloadUrl);
                
                if (response == null || response.Length == 0)
                {
                    Logger.Error("PluginRepositoryService", "Le téléchargement a retourné des données vides");
                    return false;
                }
                
                // S'assurer que le dossier de destination existe
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllBytesAsync(destinationPath, response);
                
                Logger.Info("PluginRepositoryService", $"Plugin téléchargé avec succès: {destinationPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("PluginRepositoryService", $"Erreur lors du téléchargement du plugin: {ex.Message}");
                return false;
            }
        }
    }
}

