namespace GameOverlay.Models
{
    /// <summary>
    /// Interface de base pour tous les plugins d'Amaliassistant
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Nom unique du plugin
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Version du plugin
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// Description du plugin
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Auteur du plugin
        /// </summary>
        string Author { get; }
        
        /// <summary>
        /// Initialise le plugin. Appelé une fois au chargement.
        /// </summary>
        /// <param name="context">Contexte d'exécution du plugin</param>
        void Initialize(IPluginContext context);
        
        /// <summary>
        /// Active le plugin. Appelé lorsque l'utilisateur active le plugin.
        /// </summary>
        void Activate();
        
        /// <summary>
        /// Désactive le plugin. Appelé lorsque l'utilisateur désactive le plugin.
        /// </summary>
        void Deactivate();
        
        /// <summary>
        /// Nettoie les ressources du plugin. Appelé avant le déchargement.
        /// </summary>
        void Cleanup();
        
        /// <summary>
        /// Indique si le plugin est actuellement actif
        /// </summary>
        bool IsActive { get; }
    }
}





