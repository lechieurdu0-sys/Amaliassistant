using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GameOverlay.UpdateInstaller
{
    public partial class MainWindow : Window
    {
        private string _patchUrlOrPath;
        private string _appDir;
        private string _exePath;
        private string _newVersion;
        private static readonly HttpClient _httpClient = new HttpClient();

        static MainWindow()
        {
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Amaliassistant-Updater/1.0");
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        public void Initialize(string patchUrlOrPath, string appDir, string exePath, string newVersion)
        {
            _patchUrlOrPath = patchUrlOrPath;
            _appDir = appDir;
            _exePath = exePath;
            _newVersion = newVersion;

            // Démarrer le processus de mise à jour de manière asynchrone
            Task.Run(() => ProcessUpdate());
        }

        private void ProcessUpdate()
        {
            try
            {
                UpdateStatus("Initialisation de la mise à jour...", 0);
                Thread.Sleep(500);

                // Attendre que l'application se ferme
                UpdateStatus("Attente de la fermeture de l'application...", 10);
                var waitCount = 0;
                while (waitCount < 60)
                {
                    var processes = Process.GetProcessesByName("GameOverlay.App");
                    if (processes.Length == 0)
                    {
                        UpdateStatus("Application fermée.", 20);
                        break;
                    }
                    
                    Thread.Sleep(1000);
                    waitCount++;
                    
                    if (waitCount % 5 == 0)
                    {
                        UpdateStatus($"Attente... ({waitCount}/60 secondes)", 10 + (waitCount * 10 / 60));
                    }
                }

                if (waitCount >= 60)
                {
                    UpdateStatus("ATTENTION: L'application ne se ferme pas après 60 secondes", 20);
                    UpdateStatus("Continuation de la mise à jour...", 20);
                }

                // Attendre encore 2 secondes pour être sûr que tous les fichiers sont libérés
                UpdateStatus("Attente de 2 secondes pour libérer les fichiers...", 25);
                Thread.Sleep(2000);

                // Déterminer si c'est une URL ou un chemin de fichier
                string patchPath;
                bool isUrl = _patchUrlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                            _patchUrlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                
                if (isUrl)
                {
                    // Télécharger le patch depuis l'URL
                    UpdateStatus("Téléchargement du patch...", 30);
                    patchPath = Path.Combine(Path.GetTempPath(), $"Amaliassistant_Patch_{_newVersion}.zip");
                    
                    try
                    {
                        DownloadPatch(_patchUrlOrPath, patchPath).Wait();
                        UpdateStatus("Patch téléchargé avec succès!", 50);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"ERREUR lors du téléchargement: {ex.Message}", 0);
                        Dispatcher.Invoke(() => Close());
                        return;
                    }
                }
                else
                {
                    // Utiliser le chemin de fichier fourni
                    patchPath = _patchUrlOrPath;
                }

                // Extraire le patch s'il existe
                if (File.Exists(patchPath))
                {
                    UpdateStatus("Extraction du patch...", 30);
                    
                    try
                    {
                        using (var archive = ZipFile.OpenRead(patchPath))
                        {
                            var totalEntries = archive.Entries.Count(e => !string.IsNullOrEmpty(e.Name));
                            var processedEntries = 0;
                            
                            foreach (var entry in archive.Entries)
                            {
                                if (string.IsNullOrEmpty(entry.Name))
                                    continue; // Ignorer les dossiers
                                
                                var destinationPath = Path.Combine(_appDir, entry.FullName);
                                var destinationDir = Path.GetDirectoryName(destinationPath);
                                
                                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                                {
                                    Directory.CreateDirectory(destinationDir);
                                }
                                
                                try
                                {
                                    entry.ExtractToFile(destinationPath, overwrite: true);
                                    processedEntries++;
                                    
                                    var progress = 30 + (processedEntries * 40 / totalEntries);
                                    UpdateStatus($"Extraction: {processedEntries}/{totalEntries} fichiers...", progress);
                                }
                                catch (Exception ex)
                                {
                                    UpdateStatus($"ERREUR lors de l'extraction de {entry.FullName}: {ex.Message}", 70);
                                }
                            }
                            
                            UpdateStatus($"Fichiers extraits avec succès! ({processedEntries} fichiers)", 70);
                        }
                        
                        // Supprimer le fichier patch temporaire
                        try
                        {
                            File.Delete(patchPath);
                            UpdateStatus("Fichier patch supprimé.", 75);
                        }
                        catch (Exception ex)
                        {
                            UpdateStatus($"ATTENTION: Impossible de supprimer le patch: {ex.Message}", 75);
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"ERREUR lors de l'extraction du patch: {ex.Message}", 0);
                        Dispatcher.Invoke(() => Close());
                        return;
                    }
                }
                else
                {
                    UpdateStatus("Aucun patch à extraire (fichiers déjà extraits ou patch inexistant).", 30);
                }

                // Mettre à jour la version dans le registre Windows
                UpdateStatus("Mise à jour de la version dans le registre Windows...", 80);
                
                try
                {
                    var uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D}";
                    
                    // HKCU
                    using (var key = Registry.CurrentUser.OpenSubKey(uninstallKey, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisplayVersion", _newVersion, RegistryValueKind.String);
                            key.SetValue("Version", _newVersion, RegistryValueKind.String);
                            UpdateStatus($"Version HKCU mise à jour: {_newVersion}", 85);
                        }
                    }
                    
                    // HKLM (nécessite des droits admin, on essaie quand même)
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(uninstallKey, true))
                        {
                            if (key != null)
                            {
                                key.SetValue("DisplayVersion", _newVersion, RegistryValueKind.String);
                                key.SetValue("Version", _newVersion, RegistryValueKind.String);
                                UpdateStatus($"Version HKLM mise à jour: {_newVersion}", 87);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Pas de droits admin, c'est normal pour une installation utilisateur
                        UpdateStatus("Installation utilisateur (HKLM non modifié, normal)", 87);
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"ATTENTION: Erreur lors de la mise à jour du registre: {ex.Message}", 87);
                }

                // Redémarrer l'application
                UpdateStatus("Redémarrage de l'application...", 90);
                
                if (!File.Exists(_exePath))
                {
                    UpdateStatus($"ERREUR: Le fichier exécutable est introuvable: {_exePath}", 0);
                    Dispatcher.Invoke(() => Close());
                    return;
                }

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(_exePath)
                    };
                    
                    Process.Start(startInfo);
                    UpdateStatus("Application redémarrée avec succès!", 95);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"ERREUR lors du redémarrage de l'application: {ex.Message}", 0);
                    Dispatcher.Invoke(() => Close());
                    return;
                }

                // Attendre un peu pour vérifier que l'application démarre
                Thread.Sleep(2000);

                UpdateStatus("Mise à jour terminée!", 100);
                Thread.Sleep(1500);

                Dispatcher.Invoke(() => Close());
            }
            catch (Exception ex)
            {
                UpdateStatus($"ERREUR CRITIQUE: {ex.Message}", 0);
                UpdateStatus(ex.StackTrace ?? "", 0);
                Thread.Sleep(3000);
                Dispatcher.Invoke(() => Close());
            }
        }

        private async Task DownloadPatch(string url, string destinationPath)
        {
            try
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        var totalBytesRead = 0L;
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            
                            // Mettre à jour la progression
                            if (totalBytes > 0)
                            {
                                var progress = 30 + ((double)totalBytesRead * 20 / totalBytes);
                                var mbRead = totalBytesRead / (1024 * 1024);
                                var mbTotal = totalBytes / (1024 * 1024);
                                UpdateStatus($"Téléchargement: {mbRead} MB / {mbTotal} MB", progress);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors du téléchargement du patch: {ex.Message}", ex);
            }
        }

        private void UpdateStatus(string message, double progress)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text += $"{DateTime.Now:HH:mm:ss} - {message}\n";
                ProgressBar.Value = progress;
                ProgressTextBlock.Text = message;
                
                // Faire défiler vers le bas
                StatusScrollViewer.ScrollToEnd();
            });
        }
    }
}

