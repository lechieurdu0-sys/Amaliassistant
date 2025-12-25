# Récapitulatif des problèmes du système de mise à jour - Amaliassistant

**Date de création** : 25/12/2025  
**Dernière mise à jour** : 25/12/2025  
**Version testée** : 1.0.0.22 → 1.0.0.23

---

## PROBLÈME ACTUEL (25/12/2025)

### Description
- ✅ L'application se lance correctement
- ✅ La mise à jour s'initialise correctement
- ✅ L'application se ferme correctement
- ❌ **Aucune fenêtre CMD ne s'affiche** lors de la mise à jour
- ❌ L'application ne redémarre pas automatiquement

### Code actuel (UpdateService.cs, lignes 609-624)
```csharp
var launcherInfo = new ProcessStartInfo
{
    FileName = "cmd.exe",
    Arguments = $"/k \"{launcherScriptPath}\"",
    UseShellExecute = true,
    CreateNoWindow = false,
    WindowStyle = ProcessWindowStyle.Normal
};

Logger.Info("UpdateService", $"Lancement du script batch: {launcherScriptPath}");
var scriptProcess = Process.Start(launcherInfo);
```

### Script batch créé
- **Emplacement** : `%TEMP%\Amaliassistant_PatchInstaller.bat`
- **Contenu** : Script avec `@echo off`, `title`, messages `echo`, attente de fermeture, extraction, mise à jour du registre, redémarrage avec `start "" ""{escapedExePath}""`
- **Dernière ligne** : `exit` (sans `/b`) pour fermer la fenêtre après le timeout

---

## HISTORIQUE DES PROBLÈMES ET TENTATIVES

### 1. Double proposition de mise à jour
**Problème** : La proposition de mise à jour apparaissait deux fois.  
**Cause** : Initialisation multiple du service.  
**Solution** : Ajout du flag `_isInitialized` pour éviter les initialisations multiples.

### 2. Erreur PowerShell - `.dll` vs `.exe`
**Problème** : "Aucune application n'est associée au fichier spécifié" - Tentative de lancer `.dll` au lieu de `.exe`.  
**Solution** : Conversion du chemin `.dll` en `.exe` avec `Replace(".dll", ".exe", StringComparison.OrdinalIgnoreCase)`.

### 3. Fenêtres PowerShell bloquantes/invisibles
**Problème** : 
- Fenêtres PowerShell qui bloquaient
- Fenêtres PowerShell cachées mais toujours présentes
- Sortie PowerShell non visible

**Tentatives** :
- `-WindowStyle Hidden` dans PowerShell
- `CreateNoWindow = true` dans ProcessStartInfo
- Remplacement complet de PowerShell par des scripts batch

**Résultat** : Migration vers des scripts batch pour plus de simplicité et de contrôle.

### 4. Fenêtres CMD multiples ou vides
**Problème** : 
- Deux fenêtres CMD se lançaient simultanément
- Fenêtres CMD vides apparaissaient
- La fenêtre CMD se fermait avant d'exécuter le script

**Tentatives** :
- Utilisation de `cmd.exe /c` pour lancer le script
- Modification de `start /B` vers `start ""` pour le lancement de l'app
- Suppression du script temporaire de nettoyage pour éviter la deuxième fenêtre
- Modification de `exit /b 0` vers `exit` dans le script batch
- Ajout de `timeout` pour introduire des délais

**Résultat** : Aucune solution satisfaisante trouvée.

### 5. Application ne redémarre pas
**Problème** : L'application se ferme mais ne redémarre pas automatiquement.

**Tentatives** :
- `start /B "" "{exePath}"` → L'application ne démarre pas
- `"{exePath}" &` → L'application ne démarre pas, la CMD se ferme
- `start "" "{exePath}"` → Ouvre une nouvelle fenêtre CMD
- `start "" ""{exePath}""` → Tentative actuelle

**Résultat** : Le problème persiste.

### 6. Version incorrecte dans le Panneau de configuration
**Problème** : Après mise à jour de 1.0.0.22 vers 1.0.0.23, le Panneau de configuration affiche toujours 1.0.0.22, causant une boucle de mise à jour infinie.

**Cause** : Le script batch n'actualisait pas les clés de registre Windows après l'extraction du patch.

**Solution** : Ajout de commandes `reg add` dans le script batch pour mettre à jour `DisplayVersion` et `Version` dans HKCU et HKLM :
```batch
reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D}" /v DisplayVersion /t REG_SZ /d "{version}" /f
```

### 7. Message de bienvenue persistant
**Problème** : Le message "Bienvenue sur Amaliassistant !" apparaissait après chaque mise à jour.

**Solution** : Commentaire de l'appel à `CheckAndShowWelcomeMessage()` dans `MainWindow.xaml.cs` et modification de la fonction pour ne créer que le flag sans afficher de MessageBox.

### 8. Fenêtre de téléchargement PowerShell visible
**Problème** : Une fenêtre PowerShell visible s'affichait pour télécharger le patch.

**Solution** : Suppression de l'utilisation de PowerShell pour le téléchargement, remplacement par `HttpClient` en C# avec une fenêtre de progression WPF.

---

## ARCHITECTURE ACTUELLE DU SYSTÈME DE MISE À JOUR

### Flux de mise à jour (Patch)

1. **Vérification** : `CheckForUpdateAsync()` → `GetUpdateInfoAsync()` → `ProcessUpdateInfo()`
2. **Téléchargement** : `DownloadAndApplyPatch()` avec `HttpClient` et progression
3. **Extraction préliminaire** : Extraction en C# avec `ZipFile.OpenRead()` pour les fichiers non verrouillés
4. **Création du script batch** : Génération de `Amaliassistant_PatchInstaller.bat` dans `%TEMP%`
5. **Lancement du script** : `Process.Start()` avec `cmd.exe /k` et `WindowStyle.Normal`
6. **Fermeture de l'app** : `Environment.Exit(0)` après un délai de 2 secondes
7. **Exécution du script batch** :
   - Attente de la fermeture de l'application (max 60 secondes)
   - Extraction des fichiers verrouillés restants avec `tar.exe` ou PowerShell
   - Mise à jour du registre Windows
   - Redémarrage de l'application avec `start "" ""{exePath}""`
   - Nettoyage et fermeture

### Configuration actuelle du lancement du script

```csharp
// Ligne 610-617 dans UpdateService.cs
var launcherInfo = new ProcessStartInfo
{
    FileName = "cmd.exe",
    Arguments = $"/k \"{launcherScriptPath}\"",
    UseShellExecute = true,
    CreateNoWindow = false,
    WindowStyle = ProcessWindowStyle.Normal
};
```

**Problème identifié** : Malgré `CreateNoWindow = false` et `WindowStyle = ProcessWindowStyle.Normal`, la fenêtre CMD ne s'affiche pas.

### Hypothèses sur le problème actuel

1. **Timing** : Le script batch est lancé mais l'application se ferme (`Environment.Exit(0)`) avant que la fenêtre CMD ne soit visible
2. **Permissions** : Problème de permissions empêchant la création de la fenêtre
3. **Processus parent** : La fenêtre CMD est créée mais cachée car le processus parent (l'application) se ferme immédiatement
4. **UseShellExecute** : Conflit entre `UseShellExecute = true` et `cmd.exe /k`
5. **Délai insuffisant** : Le `Task.Delay(2000)` n'est pas suffisant pour que la fenêtre soit visible

---

## PISTES DE SOLUTION NON TESTÉES

### Solution 1 : Lancer le script AVANT de fermer l'application
**Idée** : Créer un délai plus long ou attendre que le processus CMD soit complètement lancé avant `Environment.Exit(0)`.

```csharp
var scriptProcess = Process.Start(launcherInfo);
if (scriptProcess != null)
{
    // Attendre que le processus soit réellement lancé
    scriptProcess.WaitForInputIdle(5000);
    // Ou vérifier que la fenêtre existe
    await Task.Delay(3000); // Délai plus long
}
```

### Solution 2 : Utiliser `cmd.exe /c start` au lieu de `cmd.exe /k`
**Idée** : Créer une nouvelle fenêtre CMD indépendante.

```csharp
Arguments = $"/c start \"Mise à jour\" cmd.exe /k \"{launcherScriptPath}\""
```

### Solution 3 : Utiliser un script VBS pour lancer le batch
**Idée** : Créer un script VBS qui lance le batch dans une nouvelle fenêtre CMD visible.

```vbs
Set WshShell = CreateObject("WScript.Shell")
WshShell.Run "cmd.exe /k """ & scriptPath & """", 1, False
```

### Solution 4 : Utiliser `CreateProcess` avec `CREATE_NEW_CONSOLE`
**Idée** : Utiliser l'API Windows directement avec `CREATE_NEW_CONSOLE` pour forcer la création d'une nouvelle console.

### Solution 5 : Désactiver `Environment.Exit(0)` et utiliser `Application.Shutdown()`
**Idée** : Utiliser le mécanisme de fermeture WPF standard au lieu de `Environment.Exit(0)` brutal.

```csharp
WpfApplication.Current.Dispatcher.Invoke(() =>
{
    WpfApplication.Current.Shutdown(0);
});
```

### Solution 6 : Créer un exécutable séparé pour gérer la mise à jour
**Idée** : Créer un petit exécutable `.exe` (en C# ou C++) qui gère uniquement la mise à jour, au lieu d'un script batch.

---

## FICHIERS CONCERNÉS

- **`GameOverlay.App/Services/UpdateService.cs`** : Service principal de mise à jour
  - Lignes 326-715 : `DownloadAndApplyPatch()` (mise à jour par patch)
  - Lignes 717-895 : `DownloadAndInstallFull()` (mise à jour par installateur complet)
  - Lignes 609-624 : Lancement du script batch pour le patch

- **Scripts batch générés dynamiquement** :
  - `%TEMP%\Amaliassistant_PatchInstaller.bat` : Script pour l'extraction du patch et redémarrage
  - `%TEMP%\Amaliassistant_UpdateLauncher.bat` : Script pour l'installation complète et redémarrage

- **`GameOverlay.App/MainWindow.xaml.cs`** : Initialisation du service de mise à jour

---

## TESTS EFFECTUÉS

1. ✅ Test de téléchargement du patch : Fonctionne
2. ✅ Test d'extraction des fichiers non verrouillés : Fonctionne
3. ✅ Test de création du script batch : Fonctionne (script créé dans %TEMP%)
4. ❌ Test d'affichage de la fenêtre CMD : **ÉCHEC** - La fenêtre ne s'affiche pas
5. ❌ Test de redémarrage automatique : **ÉCHEC** - L'application ne redémarre pas

---

## COMMANDES UTILES POUR LE DÉBOGAGE

### Vérifier si le script batch existe
```powershell
Test-Path "$env:TEMP\Amaliassistant_PatchInstaller.bat"
Get-Content "$env:TEMP\Amaliassistant_PatchInstaller.bat"
```

### Vérifier si le flag d'exécution existe
```powershell
Test-Path "$env:TEMP\Amaliassistant_Update_Running.flag"
```

### Vérifier les processus en cours
```powershell
Get-Process | Where-Object {$_.ProcessName -like "*cmd*" -or $_.ProcessName -like "*GameOverlay*"}
```

### Lancer le script batch manuellement pour tester
```cmd
cd %TEMP%
Amaliassistant_PatchInstaller.bat
```

---

## NOTES IMPORTANTES

- Le système fonctionne correctement jusqu'à la création et au lancement du script batch
- Le problème semble être lié à la visibilité de la fenêtre CMD lors du lancement depuis une application WPF qui se ferme
- L'utilisation de `Environment.Exit(0)` peut interrompre les processus enfants avant qu'ils ne soient visibles
- Windows peut empêcher l'affichage de nouvelles fenêtres console si le processus parent se ferme trop rapidement

---

## CONCLUSION

Le système de mise à jour est fonctionnel à 80%, mais deux problèmes critiques persistent :
1. **La fenêtre CMD ne s'affiche pas** lors de la mise à jour, empêchant l'utilisateur de voir la progression
2. **L'application ne redémarre pas automatiquement** après la mise à jour

Ces problèmes sont probablement liés à la manière dont le processus batch est lancé depuis une application WPF qui se ferme immédiatement avec `Environment.Exit(0)`. La solution nécessite probablement une refonte de l'approche de lancement du script batch ou l'utilisation d'un mécanisme différent pour gérer la mise à jour.

