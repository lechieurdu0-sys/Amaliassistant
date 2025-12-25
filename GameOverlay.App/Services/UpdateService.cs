using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using GameOverlay.Kikimeter.Services;
using GameOverlay.Models;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace GameOverlay.App.Services
{
    public static class UpdateService
    {
        private const string GitHubRepo = "lechieurdu0-sys/Amaliassistant";
        private const string UpdateXmlUrl = $"https://github.com/{GitHubRepo}/releases/latest/download/update.xml";
        private const string GitHubApiReleasesUrl = $"https://api.github.com/repos/{GitHubRepo}/releases";
        private static readonly HttpClient _httpClient = new HttpClient();
        private static bool _isChecking = false;
        private static bool _isInitialized = false;
        private static bool _isUpdating = false; // Flag pour indiquer qu'une mise à jour est en cours
        
        /// <summary>
        /// Indique si une mise à jour est en cours
        /// </summary>
        public static bool IsUpdating => _isUpdating;

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Amaliassistant-Updater/1.0");
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Timeout pour les gros fichiers (installateur)
        }
        
        // HttpClient séparé pour update.xml avec timeout court
        private static HttpClient CreateQuickHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Amaliassistant-Updater/1.0");
            client.Timeout = TimeSpan.FromSeconds(5); // Timeout court pour update.xml
            return client;
        }

        /// <summary>
        /// Initialise le service de mise à jour automatique
        /// </summary>
        public static void Initialize()
        {
            // Éviter les initialisations multiples
            if (_isInitialized)
            {
                Logger.Info("UpdateService", "Service de mise à jour déjà initialisé, ignoré");
                return;
            }
            
            try
            {
                _isInitialized = true;
                Logger.Info("UpdateService", "Service de mise à jour initialisé");
                
                // Vérifier les mises à jour de manière asynchrone après un court délai (1s pour laisser l'UI s'initialiser)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000);
                        Logger.Info("UpdateService", "Délai d'initialisation écoulé, démarrage de la vérification des mises à jour");
                        CheckForUpdateAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("UpdateService", $"Erreur lors de l'appel à CheckForUpdateAsync: {ex.Message}");
                        Logger.Error("UpdateService", $"Stack trace: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors de l'initialisation du service de mise à jour: {ex.Message}");
                Logger.Error("UpdateService", $"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Vérifie manuellement les mises à jour
        /// </summary>
        public static void CheckForUpdateAsync()
        {
            if (_isChecking)
            {
                Logger.Info("UpdateService", "Une vérification de mise à jour est déjà en cours");
                return;
            }

            _isChecking = true;
            Task.Run(async () =>
            {
                try
                {
                    Logger.Info("UpdateService", $"Vérification des mises à jour depuis: {UpdateXmlUrl}");
                    
                    var updateInfo = await GetUpdateInfoAsync();
                    if (updateInfo != null)
                    {
                        Logger.Info("UpdateService", $"Informations de mise à jour récupérées: Version {updateInfo.Version}");
                        // Traiter directement sans passer par le Dispatcher pour plus de rapidité
                        ProcessUpdateInfo(updateInfo);
                    }
                    else
                    {
                        Logger.Info("UpdateService", "Aucune mise à jour disponible ou erreur lors de la récupération");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("UpdateService", $"Erreur lors de la vérification des mises à jour: {ex.Message}");
                    Logger.Error("UpdateService", $"Stack trace: {ex.StackTrace}");
                }
                finally
                {
                    _isChecking = false;
                }
            });
        }

        private static async Task<UpdateInfo?> GetUpdateInfoAsync()
        {
            try
            {
                // Utiliser un HttpClient avec timeout court pour update.xml
                using (var quickClient = CreateQuickHttpClient())
                {
                    var response = await quickClient.GetStringAsync(UpdateXmlUrl);
                var doc = XDocument.Parse(response);
                var item = doc.Element("item");
                
                if (item == null)
                    return null;

                var version = item.Element("version")?.Value;
                var url = item.Element("url")?.Value;
                var patchUrl = item.Element("patch_url")?.Value;
                var changelog = item.Element("changelog")?.Value;
                var mandatoryStr = item.Element("mandatory")?.Value;
                var mandatory = mandatoryStr?.ToLower() == "true";

                if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(url))
                    return null;

                return new UpdateInfo
                {
                    Version = version,
                    DownloadUrl = url,
                    PatchUrl = string.IsNullOrWhiteSpace(patchUrl) ? null : patchUrl,
                    ChangelogUrl = changelog,
                    Mandatory = mandatory
                };
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Info("UpdateService", "Timeout lors de la récupération de update.xml (5s) - Vérification annulée");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors de la récupération des informations de mise à jour: {ex.Message}");
                return null;
            }
        }

        private static void ProcessUpdateInfo(UpdateInfo updateInfo)
        {
            try
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null)
                {
                    Logger.Error("UpdateService", "Impossible de récupérer la version actuelle de l'assembly");
                    return;
                }

                var newVersion = ParseVersion(updateInfo.Version);

                if (newVersion == null)
                {
                    Logger.Error("UpdateService", $"Impossible de parser la version: {updateInfo.Version}");
                    return;
                }

                Logger.Info("UpdateService", $"Comparaison de versions - Actuelle: {currentVersion}, Nouvelle: {newVersion}");

                var comparison = CompareVersions(currentVersion, newVersion);
                if (comparison >= 0)
                {
                    Logger.Info("UpdateService", $"Aucune mise à jour disponible. Version actuelle: {currentVersion}, Version disponible: {newVersion} (comparison: {comparison})");
                    return;
                }

                Logger.Info("UpdateService", $"Mise à jour disponible: {currentVersion} -> {newVersion}");

                // Afficher le dialogue de mise à jour rapidement (sur le thread UI)
                // Vérifier que le Dispatcher est disponible
                if (WpfApplication.Current?.Dispatcher == null)
                {
                    Logger.Error("UpdateService", "Dispatcher non disponible, impossible d'afficher le dialogue de mise à jour");
                    // Réessayer après un court délai
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        if (WpfApplication.Current?.Dispatcher != null)
                        {
                            ProcessUpdateInfo(updateInfo);
                        }
                        else
                        {
                            Logger.Error("UpdateService", "Dispatcher toujours non disponible après délai");
                        }
                    });
                    return;
                }

                // Utiliser BeginInvoke pour ne pas bloquer le thread
                WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        Logger.Info("UpdateService", "Affichage du dialogue de mise à jour");
                var message = $"Une nouvelle version est disponible !\n\n" +
                             $"Version actuelle: {currentVersion}\n" +
                             $"Nouvelle version: {newVersion}\n\n" +
                             $"Souhaitez-vous télécharger et installer la mise à jour maintenant ?";

                var result = WpfMessageBox.Show(
                    message,
                    "Mise à jour disponible",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                            // Afficher la fenêtre de progression et lancer la mise à jour
                            Logger.Info("UpdateService", "Démarrage de la mise à jour avec fenêtre de progression");
                            var progressWindow = new UpdateProgressWindow();
                            progressWindow.Show();
                            
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await DownloadAndInstallUpdate(updateInfo, progressWindow);
                                }
                                catch (OperationCanceledException)
                                {
                                    Logger.Info("UpdateService", "Mise à jour annulée par l'utilisateur");
                                    WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        progressWindow?.Close();
                                    }));
                                }
                                catch (Exception ex)
                                {
                                    AdvancedLogger.Error(AdvancedLogger.Categories.Network, "UpdateService", $"Erreur lors de la mise à jour: {ex.Message}", ex);
                                    // Afficher un message d'erreur à l'utilisateur
                                    WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        progressWindow?.Close();
                                        WpfMessageBox.Show(
                                            $"Erreur lors de la mise à jour: {ex.Message}\n\n" +
                                            "Veuillez réessayer plus tard ou télécharger manuellement depuis GitHub.",
                                            "Erreur de mise à jour",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                                    }));
                                }
                            });
                        }
                        else
                        {
                            Logger.Info("UpdateService", "Mise à jour refusée par l'utilisateur");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("UpdateService", $"Erreur lors de l'affichage du dialogue de mise à jour: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors du traitement des informations de mise à jour: {ex.Message}");
            }
        }

        private static async Task DownloadAndInstallUpdate(UpdateInfo updateInfo, UpdateProgressWindow? progressWindow = null)
        {
            try
            {
                // Préférer le patch si disponible (plus petit)
                if (!string.IsNullOrEmpty(updateInfo.PatchUrl))
                {
                    Logger.Info("UpdateService", $"Mise à jour incrémentale disponible, téléchargement du patch depuis: {updateInfo.PatchUrl}");
                    progressWindow?.SetStatus("Téléchargement du patch de mise à jour...");
                    await DownloadAndApplyPatch(updateInfo, progressWindow);
                }
                else
                {
                    Logger.Info("UpdateService", $"Aucun patch disponible, téléchargement de l'installateur complet depuis: {updateInfo.DownloadUrl}");
                    progressWindow?.SetStatus("Téléchargement de l'installateur complet...");
                    await DownloadAndInstallFull(updateInfo, progressWindow);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors du téléchargement/installation de la mise à jour: {ex.Message}");
                // En mode silencieux, on log juste l'erreur sans afficher de MessageBox
                // L'utilisateur pourra vérifier manuellement les mises à jour si nécessaire
            }
        }

        private static async Task DownloadAndApplyPatch(UpdateInfo updateInfo, UpdateProgressWindow? progressWindow = null)
        {
            try
            {
                var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(appDir))
                {
                    throw new Exception("Impossible de déterminer le répertoire d'installation");
                }

                var tempPatchPath = Path.Combine(Path.GetTempPath(), $"Amaliassistant_Patch_{updateInfo.Version}.zip");
                
                Logger.Info("UpdateService", $"Téléchargement du patch depuis: {updateInfo.PatchUrl}");
                progressWindow?.SetStatus("Téléchargement du patch...");
                progressWindow?.SetDownloading(true);
                
                // Vérifier l'annulation
                if (progressWindow?.IsCancelled == true)
                {
                    throw new OperationCanceledException("Téléchargement annulé par l'utilisateur");
                }
                
                // Télécharger le patch avec gestion d'erreur HTTP et progression
                HttpResponseMessage? response = null;
                try
                {
                    response = await _httpClient.GetAsync(updateInfo.PatchUrl, HttpCompletionOption.ResponseHeadersRead);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            Logger.Info("UpdateService", $"Patch non trouvé (404), utilisation de l'installateur complet");
                            throw new Exception("Patch non disponible, utilisation de l'installateur complet");
                        }
                        response.EnsureSuccessStatusCode();
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    Logger.Error("UpdateService", $"Erreur HTTP lors du téléchargement du patch: {httpEx.Message}");
                    throw new Exception("Impossible de télécharger le patch, utilisation de l'installateur complet", httpEx);
                }

                // Télécharger avec progression
                var totalBytes = response!.Content.Headers.ContentLength ?? 0;
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPatchPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // Vérifier l'annulation
                        if (progressWindow?.IsCancelled == true)
                        {
                            throw new OperationCanceledException("Téléchargement annulé par l'utilisateur");
                        }
                        
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        
                        // Mettre à jour la progression
                        if (totalBytes > 0)
                        {
                            var progress = (double)totalBytesRead * 100 / totalBytes;
                            progressWindow?.SetProgress(progress, $"Téléchargé: {totalBytesRead / (1024 * 1024)} MB / {totalBytes / (1024 * 1024)} MB");
                        }
                    }
                }

                // Vérifier que le fichier existe et n'est pas vide
                var fileInfo = new FileInfo(tempPatchPath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    throw new Exception("Le patch téléchargé est vide ou invalide");
                }

                Logger.Info("UpdateService", $"Patch téléchargé ({fileInfo.Length / 1024} KB), extraction en cours...");
                
                // Marquer qu'une mise à jour est en cours pour éviter les dialogues de confirmation
                _isUpdating = true;
                
                // Extraire le patch directement en C# AVANT de fermer l'application
                // On va extraire tous les fichiers qui ne sont pas verrouillés
                progressWindow?.SetStatus("Extraction du patch...");
                progressWindow?.SetProgress(60, "Extraction du patch en cours...");
                
                var extractedFiles = new List<string>();
                var failedFiles = new List<string>();
                
                try
                {
                    using (var archive = ZipFile.OpenRead(tempPatchPath))
                    {
                        if (archive.Entries.Count == 0)
                        {
                            throw new Exception("Le patch ZIP est vide");
                        }
                        
                        Logger.Info("UpdateService", $"Patch contient {archive.Entries.Count} fichier(s), extraction...");
                        
                        var totalEntries = archive.Entries.Count;
                        var processedEntries = 0;
                        
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name))
                                continue; // Ignorer les dossiers
                            
                            var destinationPath = Path.Combine(appDir, entry.FullName);
                            var destinationDir = Path.GetDirectoryName(destinationPath);
                            
                            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                            {
                                Directory.CreateDirectory(destinationDir);
                            }
                            
                            try
                            {
                                // Essayer d'extraire chaque fichier
                                entry.ExtractToFile(destinationPath, overwrite: true);
                                extractedFiles.Add(entry.FullName);
                                Logger.Debug("UpdateService", $"Fichier extrait: {entry.FullName}");
                            }
                            catch (IOException ioEx)
                            {
                                // Fichier verrouillé, on le note et on continue
                                failedFiles.Add(entry.FullName);
                                Logger.Info("UpdateService", $"Fichier verrouillé, sera extrait au redémarrage: {entry.FullName} - {ioEx.Message}");
                            }
                            
                            processedEntries++;
                            var progress = 60 + (processedEntries * 20.0 / totalEntries);
                            progressWindow?.SetProgress(progress, $"Extraction: {processedEntries}/{totalEntries} fichiers...");
                        }
                        
                        Logger.Info("UpdateService", $"Extraction terminée: {extractedFiles.Count} fichiers extraits, {failedFiles.Count} fichiers en attente");
                    }
                }
                catch (InvalidDataException)
                {
                    throw new Exception("Le fichier téléchargé n'est pas un ZIP valide");
                }

                // TOUJOURS créer un script batch pour gérer la fermeture et le redémarrage proprement
                // Cela garantit que l'application est bien fermée avant de redémarrer
                Logger.Info("UpdateService", failedFiles.Count > 0 
                    ? $"Création d'un script pour extraire {failedFiles.Count} fichier(s) après fermeture et redémarrer..." 
                    : "Création d'un script pour redémarrer l'application après fermeture...");
                
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var exePath = assemblyLocation.Replace(".dll", ".exe", StringComparison.OrdinalIgnoreCase);
                
                if (!File.Exists(exePath))
                {
                    throw new Exception($"Le fichier exécutable est introuvable: {exePath}");
                }
                
                var launcherScriptPath = Path.Combine(Path.GetTempPath(), "Amaliassistant_PatchInstaller.bat");
                var escapedPatchPath = tempPatchPath.Replace("%", "%%").Replace("\"", "\"\"");
                var escapedAppDir = appDir.Replace("%", "%%").Replace("\"", "\"\"");
                var escapedExePath = exePath.Replace("%", "%%").Replace("\"", "\"\"");
                var escapedLauncherScriptPath = launcherScriptPath.Replace("%", "%%").Replace("\"", "\"\"");
                
                // Script batch qui attend la fermeture, extrait les fichiers restants si nécessaire, puis redémarre
                var launcherScriptContent = $@"@echo off
title Mise a jour d'Amaliassistant
setlocal enabledelayedexpansion

REM Empêcher l'exécution multiple
if exist ""%TEMP%\Amaliassistant_Update_Running.flag"" (
    exit /b 0
)
echo. > ""%TEMP%\Amaliassistant_Update_Running.flag""

echo ========================================
echo Mise a jour d'Amaliassistant
echo ========================================
echo.

REM Attendre que l'application se ferme complètement
echo Attente de la fermeture de l'application...
set WAIT_COUNT=0
:WAIT_LOOP
timeout /t 1 /nobreak >nul 2>&1
set /a WAIT_COUNT+=1
tasklist /FI ""IMAGENAME eq GameOverlay.App.exe"" 2>NUL | find /I /N ""GameOverlay.App.exe"">NUL
if not errorlevel 1 (
    if !WAIT_COUNT! LSS 60 (
        goto WAIT_LOOP
    ) else (
        echo ATTENTION: L'application ne se ferme pas apres 60 secondes
    )
)

REM Attendre encore 2 secondes pour être sûr que tous les fichiers sont libérés
echo Application fermee, attente de 2 secondes...
timeout /t 2 /nobreak

REM Si des fichiers sont verrouillés, les extraire maintenant
if exist ""{escapedPatchPath}"" (
    echo.
    echo Extraction des fichiers restants...
    
    REM Essayer avec tar.exe d'abord (disponible sur Windows 10+)
    where tar.exe >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        tar.exe -xf ""{escapedPatchPath}"" -C ""{escapedAppDir}"" --strip-components=0 >nul 2>&1
    ) else (
        REM Fallback: utiliser PowerShell (caché)
        powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -NoLogo -NonInteractive -Command ""Expand-Archive -Path '{escapedPatchPath}' -DestinationPath '{escapedAppDir}' -Force -ErrorAction SilentlyContinue"" >nul 2>&1
    )
    
    REM Supprimer le fichier patch temporaire
    del /F /Q ""{escapedPatchPath}"" >nul 2>&1
    echo Fichiers extraits avec succes!
)

echo.
echo Redemarrage de l'application...

REM Vérifier que l'exe existe
if not exist ""{escapedExePath}"" (
    echo ERREUR: Le fichier executable est introuvable: {escapedExePath}
    pause
    exit /b 1
)

REM Redémarrer l'application
echo Demarrage de l'application...
start "" ""{escapedExePath}""

REM Attendre un peu pour vérifier que l'application démarre
timeout /t 2 /nobreak

echo.
echo ========================================
echo Mise a jour terminee avec succes!
echo ========================================
echo.
echo L'application a ete redemarree.
echo.

REM Supprimer le flag d'exécution
del /F /Q ""%TEMP%\Amaliassistant_Update_Running.flag"" >nul 2>&1

REM Supprimer le script batch maintenant qu'on a terminé
if exist ""%~f0"" (
    REM Créer un script temporaire pour supprimer ce script après fermeture
    echo @echo off > ""%TEMP%\DeleteUpdateScript.bat""
    echo timeout /t 1 /nobreak >nul 2>&1 >> ""%TEMP%\DeleteUpdateScript.bat""
    echo del /F /Q ""{escapedLauncherScriptPath}"" >nul 2>&1 >> ""%TEMP%\DeleteUpdateScript.bat""
    echo del /F /Q ""%%~f0"" >nul 2>&1 >> ""%TEMP%\DeleteUpdateScript.bat""
    start /MIN "" ""%TEMP%\DeleteUpdateScript.bat""
)

REM Fermer cette fenêtre
exit /b 0
";
                File.WriteAllText(launcherScriptPath, launcherScriptContent);
                
                // Lancer le script batch directement - il s'affichera dans sa propre fenêtre
                var launcherInfo = new ProcessStartInfo
                {
                    FileName = launcherScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                
                var scriptProcess = Process.Start(launcherInfo);
                if (scriptProcess == null)
                {
                    throw new Exception("Impossible de lancer le script de redémarrage");
                }
                
                Logger.Info("UpdateService", "Script de redémarrage lancé, attente de 2 secondes avant fermeture...");
                
                Logger.Info("UpdateService", "Patch extrait, préparation du redémarrage...");
                progressWindow?.SetStatus("Mise à jour terminée !");
                progressWindow?.SetProgress(95, "L'application va se fermer et redémarrer...");
                progressWindow?.SetDownloading(false);
                
                // Attendre suffisamment longtemps pour que le script batch démarre et soit prêt
                await Task.Delay(2000);
                
                // Fermer la fenêtre de progression
                if (WpfApplication.Current?.Dispatcher != null)
                {
                    WpfApplication.Current.Dispatcher.Invoke(() =>
                    {
                        progressWindow?.Close();
                    });
                }
                
                // Attendre encore un peu pour que la fenêtre se ferme complètement
                await Task.Delay(500);
                
                // Fermer l'application immédiatement avec Environment.Exit pour éviter les dialogues
                // Le script batch va attendre que l'application se ferme, puis redémarrer
                Logger.Info("UpdateService", "Fermeture de l'application, le script va redémarrer automatiquement...");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors de l'application du patch: {ex.Message}\n{ex.StackTrace}");
                
                // Afficher un message d'erreur à l'utilisateur
                if (WpfApplication.Current?.Dispatcher != null)
                {
                    WpfApplication.Current.Dispatcher.Invoke(() =>
                    {
                        progressWindow?.Close();
                        var result = WpfMessageBox.Show(
                            $"Erreur lors de l'application du patch:\n{ex.Message}\n\n" +
                            $"Souhaitez-vous télécharger l'installateur complet à la place ?",
                            "Erreur de mise à jour",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Error);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            // Basculer sur l'installateur complet
                            Logger.Info("UpdateService", "Basculement automatique vers l'installateur complet");
                            var newProgressWindow = new UpdateProgressWindow();
                            newProgressWindow.Show();
                            
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await DownloadAndInstallFull(updateInfo, newProgressWindow);
                }
                catch (Exception fallbackEx)
                {
                    Logger.Error("UpdateService", $"Erreur lors du fallback vers l'installateur: {fallbackEx.Message}");
                                    WpfApplication.Current.Dispatcher.Invoke(() =>
                                    {
                                        newProgressWindow?.Close();
                    WpfMessageBox.Show(
                                            $"Erreur lors du téléchargement de l'installateur:\n{fallbackEx.Message}\n\n" +
                                            "Veuillez télécharger manuellement depuis GitHub.",
                        "Erreur de mise à jour",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                                    });
                                }
                            });
                        }
                    });
                }
                else
                {
                    // Si le Dispatcher n'est pas disponible, essayer quand même le fallback
                    Logger.Info("UpdateService", "Basculement automatique vers l'installateur complet (Dispatcher non disponible)");
                    try
                    {
                        await DownloadAndInstallFull(updateInfo, progressWindow);
                    }
                    catch (Exception fallbackEx)
                    {
                        Logger.Error("UpdateService", $"Erreur lors du fallback vers l'installateur: {fallbackEx.Message}");
                    }
                }
            }
        }

        private static async Task DownloadAndInstallFull(UpdateInfo updateInfo, UpdateProgressWindow? progressWindow = null)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "Amaliassistant_Setup.exe");
            
            try
            {
                Logger.Info("UpdateService", $"Téléchargement de l'installateur complet depuis: {updateInfo.DownloadUrl}");
                progressWindow?.SetStatus("Téléchargement de l'installateur...");
                progressWindow?.SetDownloading(true);
                
                // Télécharger le fichier avec gestion d'erreur et progression
                HttpResponseMessage? response = null;
                try
                {
                    response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException httpEx)
                {
                    Logger.Error("UpdateService", $"Erreur HTTP lors du téléchargement de l'installateur: {httpEx.Message}");
                    throw new Exception($"Impossible de télécharger l'installateur: {httpEx.Message}", httpEx);
                }

                // Télécharger avec progression
                var totalBytes = response!.Content.Headers.ContentLength ?? 0;
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // Vérifier l'annulation
                        if (progressWindow?.IsCancelled == true)
                        {
                            throw new OperationCanceledException("Téléchargement annulé par l'utilisateur");
                        }
                        
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        
                        // Mettre à jour la progression
                        if (totalBytes > 0)
                        {
                            var progress = (double)totalBytesRead * 100 / totalBytes;
                            progressWindow?.SetProgress(progress, $"Téléchargé: {totalBytesRead / (1024 * 1024)} MB / {totalBytes / (1024 * 1024)} MB");
                        }
                        
                        // Logger la progression toutes les 10 MB pour éviter de surcharger les logs
                        if (totalBytesRead % (10 * 1024 * 1024) < bytesRead)
                        {
                            Logger.Info("UpdateService", $"Téléchargement: {totalBytesRead / (1024 * 1024)} MB / {totalBytes / (1024 * 1024)} MB");
                        }
                    }
                }

                // Vérifier que le fichier téléchargé n'est pas vide
                var fileInfo = new FileInfo(tempPath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    throw new Exception("L'installateur téléchargé est vide ou invalide");
                }

                Logger.Info("UpdateService", $"Installateur téléchargé ({fileInfo.Length / (1024 * 1024)} MB): {tempPath}");
                progressWindow?.SetProgress(100, $"Installateur téléchargé: {fileInfo.Length / (1024 * 1024)} MB");
                progressWindow?.SetInstalling();
                
                // Marquer qu'une mise à jour est en cours pour éviter les dialogues de confirmation
                _isUpdating = true;
                
                // Préparer le redémarrage de l'application après la mise à jour
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var exePath = assemblyLocation.Replace(".dll", ".exe", StringComparison.OrdinalIgnoreCase);
                
                if (!File.Exists(exePath))
                {
                    throw new Exception($"Le fichier exécutable est introuvable: {exePath}");
                }
                
                // Créer un script batch simple pour installer et redémarrer
                var launcherScriptPath = Path.Combine(Path.GetTempPath(), "Amaliassistant_UpdateLauncher.bat");
                var escapedTempPath = tempPath.Replace("%", "%%").Replace("\"", "\"\"");
                var escapedExePath = exePath.Replace("%", "%%").Replace("\"", "\"\"");
                
                var launcherScriptContent = $@"@echo off
setlocal

REM Script simple pour installer la mise à jour et redémarrer l'application
REM Attendre que l'application se ferme
:WAIT_LOOP
timeout /t 1 /nobreak >nul 2>&1
tasklist /FI ""IMAGENAME eq GameOverlay.App.exe"" 2>NUL | find /I /N ""GameOverlay.App.exe"">NUL
if not errorlevel 1 goto WAIT_LOOP

REM Attendre un court instant supplémentaire
timeout /t 1 /nobreak >nul 2>&1

REM Lancer l'installateur en mode très silencieux
start /WAIT """" ""{escapedTempPath}"" /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /UPGRADE

REM Attendre que l'installateur se termine complètement
:WAIT_INSTALLER
timeout /t 1 /nobreak >nul 2>&1
tasklist /FI ""IMAGENAME eq Amaliassistant_Setup.exe"" 2>NUL | find /I /N ""Amaliassistant_Setup.exe"">NUL
if not errorlevel 1 goto WAIT_INSTALLER

REM Attendre un court instant supplémentaire
timeout /t 2 /nobreak >nul 2>&1

REM Redémarrer l'application
start "" ""{escapedExePath}""

REM Supprimer l'installateur temporaire et ce script
del /F /Q ""{escapedTempPath}"" >nul 2>&1
del /F /Q ""%~f0"" >nul 2>&1

endlocal
exit /b 0
";
                File.WriteAllText(launcherScriptPath, launcherScriptContent);
                
                // Lancer le script de mise à jour en arrière-plan (fenêtre cachée)
                var launcherInfo = new ProcessStartInfo
                {
                    FileName = launcherScriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                var updateProcess = Process.Start(launcherInfo);
                if (updateProcess == null)
                {
                    throw new Exception("Impossible de lancer le script de mise à jour");
                }
                
                Logger.Info("UpdateService", "Installateur prêt, redémarrage de l'application...");
                progressWindow?.SetStatus("Installation de la mise à jour...");
                progressWindow?.SetProgress(100, "L'application va se fermer et redémarrer automatiquement...");
                
                // Attendre un court instant pour que l'utilisateur voie le message
                await Task.Delay(1000);
                
                // Fermer la fenêtre de progression
                if (WpfApplication.Current?.Dispatcher != null)
                {
                    WpfApplication.Current.Dispatcher.Invoke(() =>
                    {
                        progressWindow?.Close();
                    });
                }
                
                // Fermer l'application immédiatement avec Environment.Exit pour éviter les dialogues
                Logger.Info("UpdateService", "Fermeture de l'application pour redémarrage...");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors du téléchargement/installation de l'installateur: {ex.Message}");
                
                // Supprimer le fichier temporaire s'il existe et est invalide
                try
                {
                    if (File.Exists(tempPath))
                    {
                        var fileInfo = new FileInfo(tempPath);
                        if (fileInfo.Length == 0)
                        {
                            File.Delete(tempPath);
                        }
                    }
                }
                catch { }
                
                throw; // Re-lancer l'exception pour qu'elle soit gérée par l'appelant
            }
        }

        private static Version? ParseVersion(string versionString)
        {
            if (Version.TryParse(versionString, out var version))
                return version;
            return null;
        }

        private static int CompareVersions(Version current, Version newVersion)
        {
            if (current.Major != newVersion.Major)
                return current.Major.CompareTo(newVersion.Major);
            if (current.Minor != newVersion.Minor)
                return current.Minor.CompareTo(newVersion.Minor);
            if (current.Build != newVersion.Build)
                return current.Build.CompareTo(newVersion.Build);
            return current.Revision.CompareTo(newVersion.Revision);
        }

        private class UpdateInfo
        {
            public string Version { get; set; } = string.Empty;
            public string DownloadUrl { get; set; } = string.Empty;
            public string? PatchUrl { get; set; }
            public string? ChangelogUrl { get; set; }
            public bool Mandatory { get; set; }
        }
    }
}
