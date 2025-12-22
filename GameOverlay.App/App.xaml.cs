using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GameOverlay.Kikimeter.Services;
using GameOverlay.Models;
using GameOverlay.Themes;
using GameOverlay.App.Services;

namespace GameOverlay.App
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialiser le logger
            try
            {
                Logger.Info("App", "Application démarrée");
                LootCharacterDetector.EnsureConfigFileExists();
                
                // Initialiser le service de mise à jour automatique
                UpdateService.Initialize();
            }
            catch (Exception ex)
            {
                // Si le logger plante, on ne peut rien faire
                System.Diagnostics.Debug.WriteLine($"Erreur initialisation logger: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Logger.Info("App", "Application fermée");
            }
            catch
            {
                // Si le logger plante, on ne peut rien faire
            }
            
            base.OnExit(e);
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Logger.Error("App", $"Exception non gérée: {e.Exception.Message}");
                e.Handled = true;
            }
            catch
            {
                // Si le logger plante, on ne peut rien faire
            }
        }
    }
}


