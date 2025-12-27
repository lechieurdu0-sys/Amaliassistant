# Plugin Horloge Digitale

Plugin d'exemple pour Amaliassistant qui affiche une horloge digitale déplaçable.

## Fonctionnalités

- Affichage de l'heure au format HH:mm:ss
- Fenêtre transparente (pas de fond, juste les chiffres)
- Déplaçable en cliquant et glissant
- Redimensionnable avec Ctrl + Molette de la souris (taille de police entre 12 et 200)
- Position et taille sauvegardées automatiquement

## Installation

1. Compiler le plugin :
   ```bash
   dotnet build
   ```

2. Copier la DLL dans le dossier des plugins :
   ```
   %APPDATA%\Amaliassistant\Plugins
   ```

3. Redémarrer Amaliassistant ou actualiser la liste des plugins dans le gestionnaire

## Utilisation

1. Ouvrir le Gestionnaire de plugins depuis le menu contextuel
2. Activer le plugin "Horloge Digitale"
3. L'horloge apparaîtra à l'écran

**Contrôles :**
- **Déplacer** : Cliquer et glisser la fenêtre
- **Changer la taille** : Maintenir Ctrl et utiliser la molette de la souris
- La position et la taille sont sauvegardées automatiquement

## Configuration

La configuration est sauvegardée dans :
```
%APPDATA%\Amaliassistant\Plugins\DigitalClockPlugin\config.json
```

Vous pouvez modifier ce fichier pour ajuster la position, la taille ou la couleur.




