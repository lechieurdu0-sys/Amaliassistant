using System;
using System.IO;

namespace GameOverlay.Models
{
    /// <summary>
    /// Implémentation du contexte de plugin
    /// </summary>
    public class PluginContext : IPluginContext
    {
        public string PluginDataDirectory { get; }
        public IPluginLogger Logger { get; }
        public Config ApplicationConfig { get; }
        public string PluginsDirectory { get; }
        
        public PluginContext(string pluginId, string pluginsDirectory, Config applicationConfig)
        {
            PluginsDirectory = pluginsDirectory;
            ApplicationConfig = applicationConfig;
            
            // Créer le dossier de données du plugin
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant",
                "Plugins",
                pluginId
            );
            
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }
            
            PluginDataDirectory = appDataDir;
            Logger = new PluginLogger(pluginId);
        }
    }
}

