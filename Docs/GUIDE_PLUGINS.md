# Guide des Plugins - Amaliassistant

Ce guide explique comment créer et utiliser des plugins pour Amaliassistant.

## Structure des Plugins

Les plugins sont des bibliothèques .NET (DLL) qui implémentent l'interface `IPlugin` et sont placés dans le dossier des plugins.

Le dossier des plugins se trouve à :
```
%APPDATA%\Amaliassistant\Plugins
```

## Interface IPlugin

Tous les plugins doivent implémenter l'interface `IPlugin` :

```csharp
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }
    
    void Initialize(IPluginContext context);
    void Activate();
    void Deactivate();
    void Cleanup();
    
    bool IsActive { get; }
}
```

## Contexte du Plugin

Le `IPluginContext` fourni lors de l'initialisation donne accès à :

- `PluginDataDirectory` : Dossier pour stocker les données du plugin
- `Logger` : Logger pour enregistrer des messages
- `ApplicationConfig` : Configuration de l'application
- `PluginsDirectory` : Chemin du dossier des plugins

## Exemple de Plugin Simple

Voici un exemple de plugin minimal :

```csharp
using GameOverlay.Models;
using System;
using System.IO;

namespace MyPlugin
{
    public class ExamplePlugin : IPlugin
    {
        private IPluginContext? _context;
        private bool _isActive = false;
        
        public string Name => "Exemple de Plugin";
        public string Version => "1.0.0";
        public string Description => "Un exemple simple de plugin";
        public string Author => "Votre Nom";
        
        public bool IsActive => _isActive;
        
        public void Initialize(IPluginContext context)
        {
            _context = context;
            context.Logger.Info("Plugin initialisé");
            
            // Créer un fichier de configuration dans le dossier de données du plugin
            var configPath = Path.Combine(context.PluginDataDirectory, "config.txt");
            File.WriteAllText(configPath, "Configuration du plugin");
        }
        
        public void Activate()
        {
            _isActive = true;
            _context?.Logger.Info("Plugin activé");
        }
        
        public void Deactivate()
        {
            _isActive = false;
            _context?.Logger.Info("Plugin désactivé");
        }
        
        public void Cleanup()
        {
            _context?.Logger.Info("Nettoyage du plugin");
            _context = null;
        }
    }
}
```

## Création d'un Projet de Plugin

1. Créer un nouveau projet de bibliothèque de classes .NET :

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <Reference Include="GameOverlay.Models">
      <HintPath>path\to\GameOverlay.Models.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

2. Implémenter l'interface `IPlugin`
3. Compiler le projet pour générer la DLL
4. Copier la DLL dans le dossier des plugins
5. Redémarrer Amaliassistant ou utiliser le bouton "Actualiser" dans le gestionnaire de plugins

## Gestion des Plugins

Les plugins sont gérés via le **Gestionnaire de plugins** accessible depuis :
- Le menu contextuel de l'application (icône dans la barre des tâches) → "🔌 Plugins"

Le gestionnaire permet de :
- Voir tous les plugins disponibles
- Activer/Désactiver des plugins
- Voir les informations détaillées d'un plugin
- Actualiser la liste des plugins
- Ouvrir le dossier des plugins

## Bonnes Pratiques

1. **Nommage** : Utilisez des noms de plugins uniques et descriptifs
2. **Gestion d'erreurs** : Toujours gérer les exceptions et logger les erreurs
3. **Ressources** : Nettoyez toutes les ressources dans `Cleanup()`
4. **Configuration** : Utilisez `PluginDataDirectory` pour stocker les fichiers de configuration
5. **Logging** : Utilisez le logger fourni pour toutes les opérations importantes
6. **Thread Safety** : Assurez-vous que votre plugin est thread-safe si nécessaire

## Limitations

- Les plugins sont chargés dans le même AppDomain que l'application
- Les plugins ne peuvent pas être déchargés dynamiquement (seulement au redémarrage de l'application)
- Les plugins doivent être compilés pour .NET 8.0 Windows

## Support

Pour toute question ou problème avec les plugins, consultez la documentation ou contactez le support.





