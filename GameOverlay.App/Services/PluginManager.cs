using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameOverlay.Models;

namespace GameOverlay.App.Services
{
    /// <summary>
    /// Gestionnaire de plugins pour charger et gérer les plugins dynamiquement
    /// </summary>
    public class PluginManager
    {
        private readonly Dictionary<string, IPlugin> _loadedPlugins = new();
        private readonly Dictionary<string, PluginInfo> _pluginInfos = new();
        private readonly Dictionary<string, Assembly> _pluginAssemblies = new();
        private readonly string _pluginsDirectory;
        private Config? _applicationConfig;
        private bool _isInitialized = false;
        
        /// <summary>
        /// Événement déclenché lorsqu'un plugin est chargé
        /// </summary>
        public event Action<PluginInfo>? PluginLoaded;
        
        /// <summary>
        /// Événement déclenché lorsqu'un plugin est déchargé
        /// </summary>
        public event Action<PluginInfo>? PluginUnloaded;
        
        /// <summary>
        /// Événement déclenché lorsqu'un plugin rencontre une erreur
        /// </summary>
        public event Action<PluginInfo, string>? PluginError;
        
        public PluginManager(string? pluginsDirectory = null)
        {
            // Définir le dossier des plugins (par défaut dans AppData)
            _pluginsDirectory = pluginsDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant",
                "Plugins"
            );
            
            // Créer le dossier s'il n'existe pas
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
                Logger.Info("PluginManager", $"Dossier des plugins créé: {_pluginsDirectory}");
            }
        }
        
        /// <summary>
        /// Initialise le gestionnaire de plugins avec la configuration de l'application
        /// </summary>
        public void Initialize(Config applicationConfig)
        {
            if (_isInitialized)
            {
                Logger.Info("PluginManager", "PluginManager déjà initialisé");
                return;
            }
            
            _applicationConfig = applicationConfig;
            _isInitialized = true;
            
            Logger.Info("PluginManager", $"PluginManager initialisé. Dossier des plugins: {_pluginsDirectory}");
            
            // Scanner et charger les plugins
            ScanAndLoadPlugins();
        }
        
        /// <summary>
        /// Scanne le dossier des plugins et charge ceux qui sont activés
        /// </summary>
        public void ScanAndLoadPlugins()
        {
            if (!_isInitialized || _applicationConfig == null)
            {
                Logger.Info("PluginManager", "PluginManager non initialisé");
                return;
            }
            
            Logger.Info("PluginManager", "Scan du dossier des plugins...");
            
            // Scanner les DLL dans le dossier des plugins
            var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var pluginInfo = LoadPluginInfo(pluginFile);
                    if (pluginInfo != null)
                    {
                        _pluginInfos[pluginInfo.Id] = pluginInfo;
                        
                        // Charger le plugin s'il est activé
                        if (_applicationConfig.PluginConfig.EnabledPlugins.Contains(pluginInfo.Id))
                        {
                            LoadPlugin(pluginInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("PluginManager", $"Erreur lors du scan du plugin {pluginFile}: {ex.Message}");
                }
            }
            
            Logger.Info("PluginManager", $"Scan terminé. {_pluginInfos.Count} plugin(s) trouvé(s), {_loadedPlugins.Count} plugin(s) chargé(s)");
        }
        
        /// <summary>
        /// Charge les informations d'un plugin depuis un fichier DLL
        /// </summary>
        private PluginInfo? LoadPluginInfo(string assemblyPath)
        {
            try
            {
                // Charger l'assembly en mode lecture seule
                var assembly = Assembly.LoadFrom(assemblyPath);
                
                // Rechercher les types qui implémentent IPlugin
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();
                
                if (pluginTypes.Count == 0)
                {
                    Logger.Debug("PluginManager", $"Aucun plugin trouvé dans {assemblyPath}");
                    return null;
                }
                
                if (pluginTypes.Count > 1)
                {
                    Logger.Info("PluginManager", $"Plusieurs plugins trouvés dans {assemblyPath}, seul le premier sera utilisé");
                }
                
                var pluginType = pluginTypes.First();
                
                // Créer une instance temporaire pour obtenir les informations
                var tempInstance = Activator.CreateInstance(pluginType) as IPlugin;
                if (tempInstance == null)
                {
                    Logger.Info("PluginManager", $"Impossible de créer une instance du plugin dans {assemblyPath}");
                    return null;
                }
                
                var pluginId = $"{assembly.GetName().Name}_{pluginType.Name}";
                
                var pluginInfo = new PluginInfo
                {
                    Id = pluginId,
                    Name = tempInstance.Name,
                    Version = tempInstance.Version,
                    Description = tempInstance.Description,
                    Author = tempInstance.Author,
                    AssemblyPath = assemblyPath,
                    TypeName = pluginType.FullName ?? pluginType.Name,
                    IsEnabled = _applicationConfig?.PluginConfig.EnabledPlugins.Contains(pluginId) ?? false,
                    LastLoaded = DateTime.Now
                };
                
                // Libérer l'instance temporaire
                if (tempInstance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                return pluginInfo;
            }
            catch (Exception ex)
            {
                Logger.Error("PluginManager", $"Erreur lors du chargement des informations du plugin {assemblyPath}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Charge et initialise un plugin
        /// </summary>
        public bool LoadPlugin(PluginInfo pluginInfo)
        {
            if (_loadedPlugins.ContainsKey(pluginInfo.Id))
            {
                Logger.Info("PluginManager", $"Le plugin {pluginInfo.Id} est déjà chargé");
                return true;
            }
            
            try
            {
                // Charger l'assembly
                var assembly = Assembly.LoadFrom(pluginInfo.AssemblyPath);
                _pluginAssemblies[pluginInfo.Id] = assembly;
                
                // Obtenir le type du plugin
                var pluginType = assembly.GetType(pluginInfo.TypeName);
                if (pluginType == null)
                {
                    pluginInfo.ErrorMessage = $"Type {pluginInfo.TypeName} introuvable dans l'assembly";
                    PluginError?.Invoke(pluginInfo, pluginInfo.ErrorMessage);
                    return false;
                }
                
                // Créer une instance
                var pluginInstance = Activator.CreateInstance(pluginType) as IPlugin;
                if (pluginInstance == null)
                {
                    pluginInfo.ErrorMessage = "Impossible de créer une instance du plugin";
                    PluginError?.Invoke(pluginInfo, pluginInfo.ErrorMessage);
                    return false;
                }
                
                // Créer le contexte
                var context = new PluginContext(pluginInfo.Id, _pluginsDirectory, _applicationConfig!);
                
                // Initialiser le plugin
                pluginInstance.Initialize(context);
                
                // Activer le plugin s'il est activé
                if (pluginInfo.IsEnabled)
                {
                    pluginInstance.Activate();
                }
                
                _loadedPlugins[pluginInfo.Id] = pluginInstance;
                pluginInfo.IsLoaded = true;
                pluginInfo.ErrorMessage = null;
                pluginInfo.LastLoaded = DateTime.Now;
                
                Logger.Info("PluginManager", $"Plugin chargé: {pluginInfo.Name} v{pluginInfo.Version}");
                PluginLoaded?.Invoke(pluginInfo);
                
                return true;
            }
            catch (Exception ex)
            {
                pluginInfo.ErrorMessage = ex.Message;
                pluginInfo.IsLoaded = false;
                Logger.Error("PluginManager", $"Erreur lors du chargement du plugin {pluginInfo.Id}: {ex.Message}");
                PluginError?.Invoke(pluginInfo, ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Décharge un plugin
        /// </summary>
        public bool UnloadPlugin(string pluginId)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                Logger.Info("PluginManager", $"Le plugin {pluginId} n'est pas chargé");
                return false;
            }
            
            try
            {
                // Désactiver le plugin
                if (plugin.IsActive)
                {
                    plugin.Deactivate();
                }
                
                // Nettoyer les ressources
                plugin.Cleanup();
                
                _loadedPlugins.Remove(pluginId);
                
                if (_pluginInfos.TryGetValue(pluginId, out var pluginInfo))
                {
                    pluginInfo.IsLoaded = false;
                    PluginUnloaded?.Invoke(pluginInfo);
                }
                
                Logger.Info("PluginManager", $"Plugin déchargé: {pluginId}");
                
                // Note: Dans .NET, on ne peut pas vraiment décharger une assembly sans décharger le AppDomain
                // On garde juste l'assembly en mémoire mais le plugin n'est plus utilisé
                // _pluginAssemblies.Remove(pluginId); // On garde la référence pour éviter les problèmes
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("PluginManager", $"Erreur lors du déchargement du plugin {pluginId}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Active un plugin
        /// </summary>
        public bool EnablePlugin(string pluginId)
        {
            if (!_pluginInfos.TryGetValue(pluginId, out var pluginInfo))
            {
                Logger.Info("PluginManager", $"Plugin introuvable: {pluginId}");
                return false;
            }
            
            // S'assurer que le plugin est chargé
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                if (!LoadPlugin(pluginInfo))
                {
                    return false;
                }
            }
            
            // Activer le plugin
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                plugin.Activate();
                pluginInfo.IsEnabled = true;
                
                // Ajouter à la liste des plugins activés dans la config
                if (_applicationConfig != null && !_applicationConfig.PluginConfig.EnabledPlugins.Contains(pluginId))
                {
                    _applicationConfig.PluginConfig.EnabledPlugins.Add(pluginId);
                }
                
                Logger.Info("PluginManager", $"Plugin activé: {pluginId}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Désactive un plugin
        /// </summary>
        public bool DisablePlugin(string pluginId)
        {
            if (!_pluginInfos.TryGetValue(pluginId, out var pluginInfo))
            {
                Logger.Info("PluginManager", $"Plugin introuvable: {pluginId}");
                return false;
            }
            
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                plugin.Deactivate();
                pluginInfo.IsEnabled = false;
                
                // Retirer de la liste des plugins activés
                if (_applicationConfig != null)
                {
                    _applicationConfig.PluginConfig.EnabledPlugins.Remove(pluginId);
                }
                
                Logger.Info("PluginManager", $"Plugin désactivé: {pluginId}");
                return true;
            }
            
            pluginInfo.IsEnabled = false;
            if (_applicationConfig != null)
            {
                _applicationConfig.PluginConfig.EnabledPlugins.Remove(pluginId);
            }
            
            return true;
        }
        
        /// <summary>
        /// Obtient la liste de tous les plugins (chargés ou non)
        /// </summary>
        public IEnumerable<PluginInfo> GetAllPlugins()
        {
            return _pluginInfos.Values;
        }
        
        /// <summary>
        /// Obtient un plugin spécifique
        /// </summary>
        public PluginInfo? GetPluginInfo(string pluginId)
        {
            return _pluginInfos.TryGetValue(pluginId, out var info) ? info : null;
        }
        
        /// <summary>
        /// Obtient une instance de plugin chargé
        /// </summary>
        public IPlugin? GetPlugin(string pluginId)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }
        
        /// <summary>
        /// Nettoie tous les plugins (appelé à la fermeture de l'application)
        /// </summary>
        public void CleanupAll()
        {
            Logger.Info("PluginManager", "Nettoyage de tous les plugins...");
            
            var pluginIds = _loadedPlugins.Keys.ToList();
            foreach (var pluginId in pluginIds)
            {
                try
                {
                    UnloadPlugin(pluginId);
                }
                catch (Exception ex)
                {
                    Logger.Error("PluginManager", $"Erreur lors du nettoyage du plugin {pluginId}: {ex.Message}");
                }
            }
            
            _loadedPlugins.Clear();
            _pluginInfos.Clear();
            _pluginAssemblies.Clear();
        }
        
        /// <summary>
        /// Obtient le chemin du dossier des plugins
        /// </summary>
        public string PluginsDirectory => _pluginsDirectory;
    }
}

