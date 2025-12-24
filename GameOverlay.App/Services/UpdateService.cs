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
            try
            {
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

                Logger.Info("UpdateService", $"Patch téléchargé ({fileInfo.Length / 1024} KB), extraction dans: {appDir}");
                progressWindow?.SetStatus("Extraction du patch...");
                progressWindow?.SetProgress(50, $"Extraction en cours...");

                // Vérifier que c'est bien un fichier ZIP valide avant extraction
                try
                {
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(tempPatchPath))
                    {
                        if (archive.Entries.Count == 0)
                        {
                            throw new Exception("Le patch ZIP est vide");
                        }
                        Logger.Info("UpdateService", $"Patch contient {archive.Entries.Count} fichier(s)");
                    }
                }
                catch (InvalidDataException)
                {
                    throw new Exception("Le fichier téléchargé n'est pas un ZIP valide");
                }

                // Préparer le redémarrage de l'application après l'extraction du patch
                var exePath = Assembly.GetExecutingAssembly().Location;
                var exeDir = Path.GetDirectoryName(exePath);
                
                Logger.Info("UpdateService", "Patch téléchargé, préparation de l'extraction après fermeture de l'application");
                progressWindow?.SetStatus("Préparation de la mise à jour...");
                progressWindow?.SetProgress(100, "Fermeture de l'application pour appliquer le patch...");
                
                // Créer un script PowerShell avec fenêtre de progression visible
                var launcherScriptPath = Path.Combine(Path.GetTempPath(), "Amaliassistant_PatchLauncher.ps1");
                var powershellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(powershellPath))
                {
                    powershellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "powershell.exe");
                }
                
                // Échapper les chemins pour PowerShell
                var escapedPatchPath = tempPatchPath.Replace("'", "''").Replace("\"", "`\"");
                var escapedAppDir = appDir.Replace("'", "''").Replace("\"", "`\"");
                var escapedExePath = exePath.Replace("\"", "`\"");
                
                var launcherScriptContent = $@"# Script PowerShell pour extraire le patch avec progression visible
$ErrorActionPreference = ""Stop""

# Afficher une fenêtre avec progression
Add-Type -AssemblyName System.Windows.Forms
$form = New-Object System.Windows.Forms.Form
$form.Text = ""Mise à jour d'Amaliassistant""
$form.Size = New-Object System.Drawing.Size(500, 180)
$form.StartPosition = ""CenterScreen""
$form.FormBorderStyle = ""FixedDialog""
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.TopMost = $true

$labelStatus = New-Object System.Windows.Forms.Label
$labelStatus.Text = ""Attente de la fermeture de l'application...""
$labelStatus.AutoSize = $true
$labelStatus.Location = New-Object System.Drawing.Point(20, 20)
$labelStatus.Font = New-Object System.Drawing.Font(""Segoe UI"", 10, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($labelStatus)

$labelDetails = New-Object System.Windows.Forms.Label
$labelDetails.Text = """"
$labelDetails.AutoSize = $true
$labelDetails.Location = New-Object System.Drawing.Point(20, 50)
$labelDetails.Font = New-Object System.Drawing.Font(""Segoe UI"", 9)
$form.Controls.Add($labelDetails)

$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object System.Drawing.Point(20, 80)
$progressBar.Width = 450
$progressBar.Height = 25
$progressBar.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
$form.Controls.Add($progressBar)

$form.Show() | Out-Null
$form.Refresh()

function Update-Status {{
    param([string]$status, [string]$details = """")
    $form.Invoke([action]{{
        $labelStatus.Text = $status
        $labelDetails.Text = $details
        [System.Windows.Forms.Application]::DoEvents()
    }})
}}

# Attendre que l'application se ferme complètement
Update-Status ""Attente de la fermeture de l'application...""
$waitCount = 0
while ($true) {{
    try {{
        $process = Get-Process -Name ""GameOverlay.App"" -ErrorAction SilentlyContinue
        if (-not $process) {{
            break
        }}
        $waitCount++
        if ($waitCount % 4 -eq 0) {{
            Update-Status ""Attente de la fermeture de l'application..."" ""En attente... ($waitCount / 2) secondes""
        }}
    }} catch {{
        break
    }}
    Start-Sleep -Milliseconds 500
    [System.Windows.Forms.Application]::DoEvents()
}}

# Attendre un court instant supplémentaire
Update-Status ""Préparation de l'extraction...""
Start-Sleep -Seconds 2
[System.Windows.Forms.Application]::DoEvents()

# Extraire le patch avec progression
Update-Status ""Extraction du patch en cours..."", ""Veuillez patienter...""
$progressBar.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
$progressBar.MarqueeAnimationSpeed = 50
[System.Windows.Forms.Application]::DoEvents()

try {{
    # Vérifier que le fichier existe
    if (-not (Test-Path '{escapedPatchPath}')) {{
        throw ""Le fichier patch est introuvable: '{escapedPatchPath}'""
    }}
    
    # Extraire le patch
    Expand-Archive -Path '{escapedPatchPath}' -DestinationPath '{escapedAppDir}' -Force -ErrorAction Stop
    
    Update-Status ""Patch appliqué avec succès !"", ""Redémarrage de l'application...""
    $progressBar.Style = [System.Windows.Forms.ProgressBarStyle]::Continuous
    $progressBar.Value = 100
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Seconds 1
}} catch {{
    $errorMsg = $_.Exception.Message
    $form.Close()
    [System.Windows.Forms.MessageBox]::Show(""Erreur lors de l'extraction du patch:`n$errorMsg`n`nFichier: '{escapedPatchPath}'`nDestination: '{escapedAppDir}'"", ""Erreur"", ""OK"", ""Error"")
    exit 1
}}

# Supprimer le fichier temporaire du patch
try {{
    if (Test-Path '{escapedPatchPath}') {{
        Remove-Item -Path '{escapedPatchPath}' -Force -ErrorAction SilentlyContinue
    }}
}} catch {{
    # Ignorer les erreurs de suppression
}}

# Redémarrer l'application
Update-Status ""Redémarrage de l'application..."", ""L'application va se relancer dans quelques instants...""
[System.Windows.Forms.Application]::DoEvents()
Start-Sleep -Seconds 1

try {{
    # Vérifier que le fichier exe existe
    $exePath = '{escapedExePath}'
    if (-not (Test-Path $exePath)) {{
        throw ""Le fichier exécutable est introuvable: $exePath""
    }}
    
    # Lancer l'application
    Start-Process -FilePath $exePath -ErrorAction Stop
    Update-Status ""Application redémarrée !"", ""Fermeture de la fenêtre...""
    Start-Sleep -Seconds 1
}} catch {{
    $errorMsg = $_.Exception.Message
    $form.Close()
    [System.Windows.Forms.MessageBox]::Show(""Erreur lors du redémarrage de l'application:`n$errorMsg`n`nChemin: '{escapedExePath}'"", ""Erreur"", ""OK"", ""Error"")
    exit 1
}}

# Fermer la fenêtre et supprimer le script
$form.Close()
try {{
    Start-Sleep -Milliseconds 500
    Remove-Item -Path $PSCommandPath -Force -ErrorAction SilentlyContinue
}} catch {{
    # Ignorer les erreurs de suppression du script
}}
";
                File.WriteAllText(launcherScriptPath, launcherScriptContent);

                // Fermer l'application AVANT d'extraire le patch
                Logger.Info("UpdateService", "Fermeture de l'application avant l'extraction du patch");
                
                // Fermer la fenêtre de progression
                if (WpfApplication.Current?.Dispatcher != null)
                {
                    WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        progressWindow?.Close();
                    }));
                }
                
                // Lancer le script PowerShell qui gérera l'extraction et le redémarrage avec fenêtre Windows Forms visible (mais PowerShell caché)
                var launcherInfo = new ProcessStartInfo
                {
                    FileName = powershellPath,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{launcherScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                try
                {
                    // Lancer le script PowerShell
                    var process = Process.Start(launcherInfo);
                    if (process == null)
                    {
                        throw new Exception("Impossible de lancer le script PowerShell");
                    }
                    
                    Logger.Info("UpdateService", "Script de mise à jour par patch lancé, l'application redémarrera automatiquement après l'extraction");
                    
                    // Fermer l'application maintenant, avant que l'extraction ne commence
                    await Task.Delay(500); // Délai pour que le script démarre
                    
                    if (WpfApplication.Current?.Dispatcher != null)
                    {
                        WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Logger.Info("UpdateService", "Fermeture de l'application...");
                            try
                            {
                                WpfApplication.Current.Shutdown();
                            }
                            catch (Exception shutdownEx)
                            {
                                Logger.Error("UpdateService", $"Erreur lors de la fermeture: {shutdownEx.Message}");
                                // Forcer la fermeture
                                Environment.Exit(0);
                            }
                        }), System.Windows.Threading.DispatcherPriority.Send);
                    }
                    else
                    {
                        AdvancedLogger.Warning(AdvancedLogger.Categories.System, "UpdateService", "Dispatcher non disponible, fermeture forcée");
                        Environment.Exit(0);
                    }
                }
                catch (Exception patchEx)
                {
                    AdvancedLogger.Error(AdvancedLogger.Categories.System, "UpdateService", $"Erreur lors du lancement du script de patch: {patchEx.Message}", patchEx);
                    throw;
                }
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

                // Préparer le redémarrage de l'application après la mise à jour
                var exePath = Assembly.GetExecutingAssembly().Location;
                var exeDir = Path.GetDirectoryName(exePath);
                
                // Créer un script PowerShell avec fenêtre de progression visible pour l'installateur
                var launcherScriptPath = Path.Combine(Path.GetTempPath(), "Amaliassistant_UpdateLauncher.ps1");
                var powershellPathForInstaller = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(powershellPathForInstaller))
                {
                    powershellPathForInstaller = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "powershell.exe");
                }
                
                // Échapper les chemins pour PowerShell
                var escapedTempPath = tempPath.Replace("'", "''").Replace("\"", "`\"");
                var escapedExePathForInstaller = exePath.Replace("\"", "`\"");
                
                var launcherScriptContent = $@"# Script PowerShell simplifié pour installer la mise à jour
$ErrorActionPreference = ""Stop""

# Afficher une fenêtre de message simple
Add-Type -AssemblyName System.Windows.Forms
$form = New-Object System.Windows.Forms.Form
$form.Text = ""Mise à jour d'Amaliassistant""
$form.Size = New-Object System.Drawing.Size(400, 150)
$form.StartPosition = ""CenterScreen""
$form.FormBorderStyle = ""FixedDialog""
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.TopMost = $true

$label = New-Object System.Windows.Forms.Label
$label.Text = ""Installation de la mise à jour...""
$label.AutoSize = $true
$label.Location = New-Object System.Drawing.Point(20, 30)
$form.Controls.Add($label)

$form.Show() | Out-Null
$form.Refresh()

# Lancer l'installateur en mode très silencieux
$label.Text = ""Lancement de l'installateur...""
$form.Refresh()
[System.Windows.Forms.Application]::DoEvents()

try {{
    $installerProcess = Start-Process -FilePath '{escapedTempPath}' -ArgumentList ""/VERYSILENT"", ""/NORESTART"", ""/SUPPRESSMSGBOXES"", ""/UPGRADE"" -PassThru -WindowStyle Hidden
    
    # Attendre que l'installateur se termine complètement
    $label.Text = ""Installation en cours... Veuillez patienter...""
    $form.Refresh()
    [System.Windows.Forms.Application]::DoEvents()
    
    while (-not $installerProcess.HasExited) {{
        Start-Sleep -Milliseconds 500
        [System.Windows.Forms.Application]::DoEvents()
    }}
    
    # Vérifier aussi que le processus n'existe plus dans la liste
    while ($true) {{
        try {{
            $process = Get-Process -Name ""Amaliassistant_Setup"" -ErrorAction SilentlyContinue
            if (-not $process) {{
                break
            }}
        }} catch {{
            break
        }}
        Start-Sleep -Milliseconds 500
        [System.Windows.Forms.Application]::DoEvents()
    }}
    
    # Attendre un court instant supplémentaire
    Start-Sleep -Seconds 2
    
    # Redémarrer l'application
    $label.Text = ""Redémarrage de l'application...""
    $form.Refresh()
    Start-Sleep -Seconds 1
    Start-Process -FilePath '{escapedExePathForInstaller}'
    
    # Supprimer l'installateur temporaire
    try {{
        Remove-Item -Path '{escapedTempPath}' -Force -ErrorAction SilentlyContinue
    }} catch {{}}
    
    # Fermer la fenêtre et supprimer le script
    Start-Sleep -Seconds 1
    $form.Close()
    Remove-Item -Path $PSCommandPath -Force -ErrorAction SilentlyContinue
}} catch {{
    $form.Close()
    [System.Windows.Forms.MessageBox]::Show(""Erreur lors de l'installation:`n$($_.Exception.Message)"", ""Erreur"", ""OK"", ""Error"")
    exit 1
}}
";
                File.WriteAllText(launcherScriptPath, launcherScriptContent);

                // Fermer l'application AVANT de lancer le script batch
                Logger.Info("UpdateService", "Fermeture de l'application avant l'installation de la mise à jour");
                
                // Fermer la fenêtre de progression
                if (WpfApplication.Current?.Dispatcher != null)
                {
                    WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        progressWindow?.Close();
                    }));
                }
                
                // Lancer le script PowerShell qui gérera l'installation et le redémarrage avec fenêtre Windows Forms visible (mais PowerShell caché)
                var launcherInfo = new ProcessStartInfo
                {
                    FileName = powershellPathForInstaller,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{launcherScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                try
                {
                    // Lancer le script PowerShell
                    var process = Process.Start(launcherInfo);
                    if (process == null)
                    {
                        throw new Exception("Impossible de lancer le script PowerShell");
                    }
                    
                    Logger.Info("UpdateService", "Script de mise à jour lancé, l'application redémarrera automatiquement après l'installation");
                    
                    // Fermer l'application maintenant, avant que l'installateur ne démarre
                    await Task.Delay(500); // Délai pour que le script démarre
                    
                    if (WpfApplication.Current?.Dispatcher != null)
                    {
                        WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Logger.Info("UpdateService", "Fermeture de l'application...");
                            try
                            {
                                WpfApplication.Current.Shutdown();
                            }
                            catch (Exception shutdownEx)
                            {
                                Logger.Error("UpdateService", $"Erreur lors de la fermeture: {shutdownEx.Message}");
                                // Forcer la fermeture
                                Environment.Exit(0);
                            }
                        }), System.Windows.Threading.DispatcherPriority.Send);
                    }
                    else
                    {
                        AdvancedLogger.Warning(AdvancedLogger.Categories.System, "UpdateService", "Dispatcher non disponible, fermeture forcée");
                        Environment.Exit(0);
                    }
                }
                catch (Exception installEx)
                {
                    AdvancedLogger.Error(AdvancedLogger.Categories.System, "UpdateService", $"Erreur lors du lancement du script de mise à jour: {installEx.Message}", installEx);
                    throw;
                }
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
