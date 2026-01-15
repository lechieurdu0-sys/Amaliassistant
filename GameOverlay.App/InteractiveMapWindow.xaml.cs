using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using GameOverlay.Models;
// Plugin carte interactive désactivé temporairement
// using WakfuInteractiveMap.Plugin;
// using WakfuInteractiveMap.Shared.Wpf.Abstractions;
using CustomMessageBox = GameOverlay.Kikimeter.Views.CustomMessageBox;
using FormsApplication = System.Windows.Forms.Application;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace GameOverlay.App
{
    public partial class InteractiveMapWindow : Window
    {
        private readonly Config _config;
        private readonly Action _saveConfigAction;
        // private IInteractiveMapWpfPlugin? _plugin;

        public InteractiveMapWindow(Config config, Action saveConfigAction)
        {
            InitializeComponent();
            _config = config;
            _saveConfigAction = saveConfigAction;

            // Charger la vue du plugin
            LoadPluginView();

            // Charger la position sauvegardée
            LoadWindowPosition();
        }

        private void LoadPluginView()
        {
            try
            {
                // Obtenir le chemin du repo depuis la config
                var repoRoot = _config.InteractiveMapRepoRoot;

                // Si le repo n'est pas configuré, demander à l'utilisateur
                if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
                {
                    var result = CustomMessageBox.Show(
                        "Le dossier du projet PluginCarteInteractive n'est pas configuré.\n\n" +
                        "Voulez-vous le configurer maintenant ?",
                        "Configuration requise",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (SelectRepoRoot())
                        {
                            repoRoot = _config.InteractiveMapRepoRoot;
                        }
                        else
                        {
                            // Utiliser un chemin par défaut comme fallback
                            repoRoot = AppDomain.CurrentDomain.BaseDirectory;
                        }
                    }
                    else
                    {
                        // Utiliser un chemin par défaut comme fallback
                        repoRoot = AppDomain.CurrentDomain.BaseDirectory;
                    }
                }

                // Plugin carte interactive désactivé temporairement
                // Créer l'instance du plugin
                // _plugin = new WakfuInteractiveMapPlugin();
                // var view = _plugin.CreateView(repoRoot);
                // MapContentPresenter.Content = view;

                Logger.Info("InteractiveMapWindow", $"Plugin carte désactivé temporairement (repoRoot: {repoRoot})");
                CustomMessageBox.Show(
                    "La carte interactive est temporairement désactivée.\nLe plugin n'est pas disponible pour cette version.",
                    "Fonctionnalité désactivée",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("InteractiveMapWindow", $"Erreur lors du chargement du plugin: {ex.Message}");
                CustomMessageBox.Show(
                    $"Erreur lors du chargement de la carte interactive:\n\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool SelectRepoRoot()
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Sélectionner le dossier racine du projet PluginCarteInteractive";
                    dialog.ShowNewFolderButton = false;

                    // Proposer un chemin par défaut
                    var defaultPath = @"D:\Users\lechi\Desktop\Projet carte intéractive Wakfu\PluginCarteInteractive";
                    if (Directory.Exists(defaultPath))
                    {
                        dialog.SelectedPath = defaultPath;
                    }

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _config.InteractiveMapRepoRoot = dialog.SelectedPath;
                        _saveConfigAction?.Invoke();
                        Logger.Info("InteractiveMapWindow", $"RepoRoot configuré: {dialog.SelectedPath}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("InteractiveMapWindow", $"Erreur lors de la sélection du dossier: {ex.Message}");
            }

            return false;
        }

        private void ConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectRepoRoot())
            {
                // Recharger la vue du plugin avec le nouveau chemin
                LoadPluginView();

                CustomMessageBox.Show(
                    "Configuration enregistrée.\nLa carte sera rechargée.",
                    "Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    this.DragMove();
                    SaveWindowPosition();
                }
                catch
                {
                    // Ignorer les erreurs de déplacement
                }
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Permettre de déplacer la fenêtre en cliquant sur le border
            if (e.OriginalSource == sender)
            {
                TitleBar_MouseLeftButtonDown(sender, e);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveWindowPosition();
            this.Hide();
        }

        private void LoadWindowPosition()
        {
            if (_config.InteractiveMapWindowX >= 0 && _config.InteractiveMapWindowY >= 0)
            {
                this.Left = _config.InteractiveMapWindowX;
                this.Top = _config.InteractiveMapWindowY;
            }

            if (_config.InteractiveMapWindowWidth > 0 && _config.InteractiveMapWindowHeight > 0)
            {
                this.Width = _config.InteractiveMapWindowWidth;
                this.Height = _config.InteractiveMapWindowHeight;
            }
        }

        private void SaveWindowPosition()
        {
            _config.InteractiveMapWindowX = (int)this.Left;
            _config.InteractiveMapWindowY = (int)this.Top;
            _config.InteractiveMapWindowWidth = this.Width;
            _config.InteractiveMapWindowHeight = this.Height;
            _saveConfigAction?.Invoke();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            SaveWindowPosition();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged || sizeInfo.HeightChanged)
            {
                SaveWindowPosition();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SaveWindowPosition();
        }
    }
}

