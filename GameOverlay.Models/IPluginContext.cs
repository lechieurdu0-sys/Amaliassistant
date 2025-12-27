using System;
using System.Windows;

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
        
        /// <summary>
        /// Sauvegarde la position d'une fenêtre du plugin
        /// </summary>
        /// <param name="windowId">Identifiant unique de la fenêtre (ex: "MainWindow", "ClockWindow")</param>
        /// <param name="left">Position X</param>
        /// <param name="top">Position Y</param>
        /// <param name="width">Largeur (optionnel)</param>
        /// <param name="height">Hauteur (optionnel)</param>
        void SaveWindowPosition(string windowId, double left, double top, double? width = null, double? height = null);
        
        /// <summary>
        /// Charge la position sauvegardée d'une fenêtre du plugin
        /// </summary>
        /// <param name="windowId">Identifiant unique de la fenêtre</param>
        /// <returns>La position sauvegardée ou null si aucune position n'est sauvegardée</returns>
        PluginWindowPosition? LoadWindowPosition(string windowId);
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





