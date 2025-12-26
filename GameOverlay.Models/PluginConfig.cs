using System.Collections.Generic;

namespace GameOverlay.Models
{
    /// <summary>
    /// Configuration des plugins
    /// </summary>
    public class PluginConfig
    {
        /// <summary>
        /// Liste des plugins activés (par ID)
        /// </summary>
        public List<string> EnabledPlugins { get; set; } = new List<string>();
        
        /// <summary>
        /// Dictionnaire des paramètres spécifiques par plugin
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> PluginSettings { get; set; } = new Dictionary<string, Dictionary<string, object>>();
    }
}

