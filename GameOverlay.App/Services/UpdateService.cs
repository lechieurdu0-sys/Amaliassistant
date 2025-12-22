using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using GameOverlay.Kikimeter.Services;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace GameOverlay.App.Services
{
    public static class UpdateService
    {
        private const string GitHubRepo = "lechieurdu0-sys/Amaliassistant";
        private const string UpdateXmlUrl = $"https://github.com/{GitHubRepo}/releases/latest/download/update.xml";
        private static readonly HttpClient _httpClient = new HttpClient();
        private static bool _isChecking = false;

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Amaliassistant-Updater/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Initialise le service de mise à jour automatique
        /// </summary>
        public static void Initialize()
        {
            try
            {
                Logger.Info("UpdateService", "Service de mise à jour initialisé");
                
                // Vérifier les mises à jour de manière asynchrone après le démarrage
                Task.Delay(3000).ContinueWith(_ =>
                {
                    WpfApplication.Current.Dispatcher.Invoke(() =>
                    {
                        CheckForUpdateAsync();
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors de l'initialisation du service de mise à jour: {ex.Message}");
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
                        WpfApplication.Current.Dispatcher.Invoke(() =>
                        {
                            ProcessUpdateInfo(updateInfo);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("UpdateService", $"Erreur lors de la vérification des mises à jour: {ex.Message}");
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
                var response = await _httpClient.GetStringAsync(UpdateXmlUrl);
                var doc = XDocument.Parse(response);
                var item = doc.Element("item");
                
                if (item == null)
                    return null;

                var version = item.Element("version")?.Value;
                var url = item.Element("url")?.Value;
                var changelog = item.Element("changelog")?.Value;
                var mandatoryStr = item.Element("mandatory")?.Value;
                var mandatory = mandatoryStr?.ToLower() == "true";

                if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(url))
                    return null;

                return new UpdateInfo
                {
                    Version = version,
                    DownloadUrl = url,
                    ChangelogUrl = changelog,
                    Mandatory = mandatory
                };
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
                var newVersion = ParseVersion(updateInfo.Version);

                if (newVersion == null)
                {
                    Logger.Error("UpdateService", $"Impossible de parser la version: {updateInfo.Version}");
                    return;
                }

                if (CompareVersions(currentVersion, newVersion) >= 0)
                {
                    Logger.Info("UpdateService", $"Aucune mise à jour disponible. Version actuelle: {currentVersion}");
                    return;
                }

                Logger.Info("UpdateService", $"Mise à jour disponible: {currentVersion} -> {newVersion}");

                // Afficher le dialogue de mise à jour
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
                    DownloadAndInstallUpdate(updateInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors du traitement des informations de mise à jour: {ex.Message}");
            }
        }

        private static async void DownloadAndInstallUpdate(UpdateInfo updateInfo)
        {
            try
            {
                Logger.Info("UpdateService", $"Téléchargement de la mise à jour depuis: {updateInfo.DownloadUrl}");

                var tempPath = Path.Combine(Path.GetTempPath(), "Amaliassistant_Setup.exe");
                
                // Télécharger le fichier
                var response = await _httpClient.GetAsync(updateInfo.DownloadUrl);
                response.EnsureSuccessStatusCode();

                using (var fileStream = new FileStream(tempPath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                Logger.Info("UpdateService", $"Mise à jour téléchargée: {tempPath}");

                // Lancer l'installateur
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true,
                    Verb = "runas" // Demander les droits admin si nécessaire
                };

                System.Diagnostics.Process.Start(processInfo);

                // Fermer l'application
                Logger.Info("UpdateService", "Fermeture de l'application pour installer la mise à jour");
                WpfApplication.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateService", $"Erreur lors du téléchargement/installation de la mise à jour: {ex.Message}");
                WpfMessageBox.Show(
                    $"Erreur lors du téléchargement de la mise à jour:\n{ex.Message}\n\n" +
                    $"Vous pouvez télécharger manuellement depuis:\n{updateInfo.ChangelogUrl}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            public string? ChangelogUrl { get; set; }
            public bool Mandatory { get; set; }
        }
    }
}
