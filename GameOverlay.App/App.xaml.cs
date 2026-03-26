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
                // Brancher les handlers globaux le plus tôt possible
                this.DispatcherUnhandledException += Application_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

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
                Logger.Error("App", $"DispatcherUnhandledException (UI): {e.Exception}");
                e.Handled = true;
            }
            catch
            {
                // Si le logger plante, on ne peut rien faire
            }
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                Logger.Error("App", $"AppDomain.UnhandledException (terminating={e.IsTerminating}): {ex}");
            }
            catch
            {
                // Dernière ligne de défense
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                Logger.Error("App", $"TaskScheduler.UnobservedTaskException: {e.Exception}");
                e.SetObserved();
            }
            catch
            {
                // Dernière ligne de défense
            }
        }
    }
}


