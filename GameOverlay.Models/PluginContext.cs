using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GameOverlay.Models
{
    /// <summary>
    /// Implémentation du contexte de plugin
    /// </summary>
    public class PluginContext : IPluginContext
    {
        private readonly string _pluginId;
        private readonly string _windowsConfigPath;
        
        public string PluginDataDirectory { get; }
        public IPluginLogger Logger { get; }
        public Config ApplicationConfig { get; }
        public string PluginsDirectory { get; }
        
        public PluginContext(string pluginId, string pluginsDirectory, Config applicationConfig)
        {
            _pluginId = pluginId;
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
            
            // Chemin du fichier de configuration des fenêtres
            _windowsConfigPath = Path.Combine(appDataDir, "windows.json");
        }
        
        /// <summary>
        /// Sauvegarde la position d'une fenêtre du plugin
        /// </summary>
        public void SaveWindowPosition(string windowId, double left, double top, double? width = null, double? height = null)
        {
            try
            {
                Dictionary<string, PluginWindowPosition> windows;
                
                // Charger les positions existantes
                if (File.Exists(_windowsConfigPath))
                {
                    try
                    {
                        var json = File.ReadAllText(_windowsConfigPath);
                        windows = JsonConvert.DeserializeObject<Dictionary<string, PluginWindowPosition>>(json) 
                            ?? new Dictionary<string, PluginWindowPosition>();
                    }
                    catch
                    {
                        windows = new Dictionary<string, PluginWindowPosition>();
                    }
                }
                else
                {
                    windows = new Dictionary<string, PluginWindowPosition>();
                }
                
                // Mettre à jour ou ajouter la position
                windows[windowId] = new PluginWindowPosition
                {
                    Left = left,
                    Top = top,
                    Width = width,
                    Height = height
                };
                
                // Sauvegarder
                var updatedJson = JsonConvert.SerializeObject(windows, Formatting.Indented);
                File.WriteAllText(_windowsConfigPath, updatedJson);
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur lors de la sauvegarde de la position de la fenêtre '{windowId}': {ex.Message}");
            }
        }
        
        /// <summary>
        /// Charge la position sauvegardée d'une fenêtre du plugin
        /// </summary>
        public PluginWindowPosition? LoadWindowPosition(string windowId)
        {
            try
            {
                if (!File.Exists(_windowsConfigPath))
                {
                    return null;
                }
                
                var json = File.ReadAllText(_windowsConfigPath);
                var windows = JsonConvert.DeserializeObject<Dictionary<string, PluginWindowPosition>>(json);
                
                if (windows != null && windows.TryGetValue(windowId, out var position))
                {
                    // Vérifier que la position est valide (valeur >= 0)
                    if (position.Left >= 0 && position.Top >= 0)
                    {
                        return position;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Erreur lors du chargement de la position de la fenêtre '{windowId}': {ex.Message}");
                return null;
            }
        }
    }
    
    /// <summary>
    /// Position et taille d'une fenêtre de plugin
    /// </summary>
    public class PluginWindowPosition
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
    }
}

