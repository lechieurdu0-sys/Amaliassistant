namespace GameOverlay.Models
{
    /// <summary>
    /// Implémentation du logger pour les plugins
    /// </summary>
    public class PluginLogger : IPluginLogger
    {
        private readonly string _pluginName;
        
        public PluginLogger(string pluginName)
        {
            _pluginName = pluginName;
        }
        
        public void Info(string message)
        {
            Logger.Info($"Plugin[{_pluginName}]", message);
        }
        
        public void Warning(string message)
        {
            Logger.Warning($"Plugin[{_pluginName}]", message);
        }
        
        public void Error(string message)
        {
            Logger.Error($"Plugin[{_pluginName}]", message);
        }
        
        public void Debug(string message)
        {
            Logger.Debug($"Plugin[{_pluginName}]", message);
        }
    }
}





