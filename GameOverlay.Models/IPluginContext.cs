using System;

namespace GameOverlay.Models
{
    /// <summary>
    /// Contexte fourni aux plugins pour interagir avec l'application
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>
        /// Chemin du dossier de données du plugin
        /// </summary>
        string PluginDataDirectory { get; }
        
        /// <summary>
        /// Logger pour enregistrer des messages
        /// </summary>
        IPluginLogger Logger { get; }
        
        /// <summary>
        /// Obtenir la configuration de l'application
        /// </summary>
        Config ApplicationConfig { get; }
        
        /// <summary>
        /// Chemin du dossier des plugins
        /// </summary>
        string PluginsDirectory { get; }
    }
    
    /// <summary>
    /// Logger spécifique pour les plugins
    /// </summary>
    public interface IPluginLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Debug(string message);
    }
}

