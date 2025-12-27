using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GameOverlay.App.Services;
using GameOverlay.Models;
using GameOverlay.Themes;
using CustomMessageBox = GameOverlay.Kikimeter.Views.CustomMessageBox;
using Point = System.Windows.Point;

namespace GameOverlay.App
{
    public partial class PluginManagerWindow : Window
    {
        private readonly PluginManager _pluginManager;
        private readonly ObservableCollection<PluginInfoViewModel> _localPlugins = new();
        private readonly ObservableCollection<PluginRepositoryViewModel> _availablePlugins = new();
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private readonly SolidColorBrush _sectionBackgroundBrush;

        public PluginManagerWindow(PluginManager pluginManager)
        {
            InitializeComponent();
            _pluginManager = pluginManager;
            LocalPluginsDataGrid.ItemsSource = _localPlugins;
            AvailablePluginsDataGrid.ItemsSource = _availablePlugins;
            LocalPluginsDataGrid.SelectionChanged += LocalPluginsDataGrid_SelectionChanged;
            AvailablePluginsDataGrid.SelectionChanged += AvailablePluginsDataGrid_SelectionChanged;
            
            // Initialiser les couleurs comme dans SettingsWindow
            var accent = ThemeManager.AccentBrush;
            var section = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00));
            section.Freeze();
            _sectionBackgroundBrush = section;
            
            ApplyTheme(accent);
            Resources["SectionBackgroundBrush"] = _sectionBackgroundBrush;
            
            Loaded += PluginManagerWindow_Loaded;
            ThemeManager.AccentColorChanged += ThemeManager_AccentColorChanged;
            RefreshLocalPluginsList();
            _ = LoadAvailablePluginsAsync();
        }

        private async void PluginManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshLocalPluginsList();
            await LoadAvailablePluginsAsync();
        }

        private void ThemeManager_AccentColorChanged(object? sender, AccentColorChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var accent = ThemeManager.AccentBrush;
                ApplyTheme(accent);
            });
        }

        private void ApplyTheme(System.Windows.Media.Brush accentBrush)
        {
            var accentClone = accentBrush.CloneCurrentValue();
            accentClone.Freeze();
            Resources["CyanAccentBrush"] = accentClone;
        }

        private void RefreshLocalPluginsList()
        {
            _localPlugins.Clear();
            
            var allPlugins = _pluginManager.GetAllPlugins().ToList();
            
            foreach (var pluginInfo in allPlugins)
            {
                var viewModel = new PluginInfoViewModel
                {
                    Id = pluginInfo.Id,
                    Name = pluginInfo.Name,
                    Version = pluginInfo.Version,
                    Description = pluginInfo.Description,
                    Author = pluginInfo.Author,
                    IsEnabled = pluginInfo.IsEnabled,
                    IsLoaded = pluginInfo.IsLoaded,
                    ErrorMessage = pluginInfo.ErrorMessage
                };
                
                _localPlugins.Add(viewModel);
            }
            
            if (_localPlugins.Count == 0)
            {
                Logger.Info("PluginManagerWindow", "Aucun plugin local trouvé");
            }
        }

        private async Task LoadAvailablePluginsAsync()
        {
            try
            {
                Logger.Info("PluginManagerWindow", "Chargement des plugins disponibles depuis GitHub...");
                
                var availablePlugins = await PluginRepositoryService.GetAvailablePluginsAsync();
                
                _availablePlugins.Clear();
                
                foreach (var plugin in availablePlugins)
                {
                    // Vérifier si le plugin est déjà installé localement
                    var localPlugin = _pluginManager.GetPluginInfo(plugin.Id);
                    plugin.IsInstalled = localPlugin != null;
                    plugin.InstalledVersion = localPlugin?.Version;
                    
                    if (localPlugin != null && !string.IsNullOrEmpty(localPlugin.Version) && !string.IsNullOrEmpty(plugin.Version))
                    {
                        // Comparer les versions (simple comparaison de strings pour l'instant)
                        plugin.HasUpdate = localPlugin.Version != plugin.Version;
                    }
                    
                    var viewModel = new PluginRepositoryViewModel
                    {
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Version = plugin.Version,
                        Description = plugin.Description,
                        Author = plugin.Author,
                        DownloadUrl = plugin.DownloadUrl,
                        IsInstalled = plugin.IsInstalled,
                        InstalledVersion = plugin.InstalledVersion,
                        HasUpdate = plugin.HasUpdate
                    };
                    
                    _availablePlugins.Add(viewModel);
                }
                
                Logger.Info("PluginManagerWindow", $"{_availablePlugins.Count} plugin(s) disponible(s) chargé(s)");
            }
            catch (Exception ex)
            {
                Logger.Error("PluginManagerWindow", $"Erreur lors du chargement des plugins disponibles: {ex.Message}");
            }
        }

        private void LocalPluginsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LocalPluginsDataGrid.SelectedItem is PluginInfoViewModel selected)
            {
                SelectedPluginName.Text = selected.Name;
                SelectedPluginDescription.Text = selected.Description;
                SelectedPluginAuthor.Text = selected.Author;
                SelectedPluginVersion.Text = selected.Version;
                SelectedPluginVersionLabel.Visibility = Visibility.Visible;
                SelectedPluginVersion.Visibility = Visibility.Visible;
                
                if (!string.IsNullOrEmpty(selected.ErrorMessage))
                {
                    SelectedPluginError.Text = selected.ErrorMessage;
                    SelectedPluginError.Visibility = Visibility.Visible;
                    SelectedPluginErrorLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    SelectedPluginError.Visibility = Visibility.Collapsed;
                    SelectedPluginErrorLabel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void AvailablePluginsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (AvailablePluginsDataGrid.SelectedItem is PluginRepositoryViewModel selected)
            {
                SelectedPluginName.Text = selected.Name;
                SelectedPluginDescription.Text = selected.Description;
                SelectedPluginAuthor.Text = selected.Author;
                SelectedPluginVersion.Text = selected.Version;
                SelectedPluginVersionLabel.Visibility = Visibility.Visible;
                SelectedPluginVersion.Visibility = Visibility.Visible;
                SelectedPluginError.Visibility = Visibility.Collapsed;
                SelectedPluginErrorLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void PluginEnabledCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && 
                checkBox.DataContext is PluginInfoViewModel pluginViewModel)
            {
                try
                {
                    bool newValue = checkBox.IsChecked ?? false;
                    
                    if (newValue)
                    {
                        _pluginManager.EnablePlugin(pluginViewModel.Id);
                    }
                    else
                    {
                        _pluginManager.DisablePlugin(pluginViewModel.Id);
                    }
                    
                    pluginViewModel.IsEnabled = newValue;
                    
                    RefreshLocalPluginsList();
                    
                    Logger.Info("PluginManagerWindow", $"Plugin {pluginViewModel.Id} {(newValue ? "activé" : "désactivé")}");
                }
                catch (Exception ex)
                {
                    Logger.Error("PluginManagerWindow", $"Erreur lors de l'activation/désactivation du plugin: {ex.Message}");
                    CustomMessageBox.Show(
                        $"Erreur lors de l'activation/désactivation du plugin:\n{ex.Message}",
                        "Erreur",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    RefreshLocalPluginsList();
                }
                
                e.Handled = true;
            }
        }

        private async void InstallPluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button &&
                button.DataContext is PluginRepositoryViewModel pluginViewModel)
            {
                try
                {
                    if (string.IsNullOrEmpty(pluginViewModel.DownloadUrl))
                    {
                        CustomMessageBox.Show(
                            "URL de téléchargement non disponible pour ce plugin.",
                            "Erreur",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                    
                    // Demander confirmation
                    var result = CustomMessageBox.Show(
                        $"Voulez-vous installer le plugin \"{pluginViewModel.Name}\" version {pluginViewModel.Version} ?",
                        "Confirmation d'installation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                        return;
                    
                    // Déterminer le nom du fichier depuis l'URL
                    var fileName = Path.GetFileName(new Uri(pluginViewModel.DownloadUrl).LocalPath);
                    if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = $"{pluginViewModel.Id}.dll";
                    }
                    
                    var destinationPath = Path.Combine(_pluginManager.PluginsDirectory, fileName);
                    
                    // Télécharger le plugin
                    var downloadResult = await PluginRepositoryService.DownloadPluginAsync(pluginViewModel.DownloadUrl, destinationPath);
                    
                    if (downloadResult)
                    {
                        CustomMessageBox.Show(
                            $"Plugin \"{pluginViewModel.Name}\" installé avec succès !\nVeuillez actualiser la liste des plugins locaux.",
                            "Installation réussie",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        
                        // Actualiser la liste des plugins locaux et disponibles
                        RefreshLocalPluginsList();
                        await LoadAvailablePluginsAsync();
                    }
                    else
                    {
                        CustomMessageBox.Show(
                            "Erreur lors du téléchargement du plugin.",
                            "Erreur",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("PluginManagerWindow", $"Erreur lors de l'installation du plugin: {ex.Message}");
                    CustomMessageBox.Show(
                        $"Erreur lors de l'installation du plugin:\n{ex.Message}",
                        "Erreur",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _pluginManager.ScanAndLoadPlugins();
            RefreshLocalPluginsList();
            CustomMessageBox.Show(
                "Liste des plugins locaux actualisée.",
                "Actualisation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void RefreshAvailablePluginsButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAvailablePluginsAsync();
            CustomMessageBox.Show(
                "Liste des plugins disponibles actualisée.",
                "Actualisation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OpenPluginsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pluginsFolder = _pluginManager.PluginsDirectory;
                
                if (!Directory.Exists(pluginsFolder))
                {
                    Directory.CreateDirectory(pluginsFolder);
                }
                
                Process.Start("explorer.exe", pluginsFolder);
            }
            catch (Exception ex)
            {
                Logger.Error("PluginManagerWindow", $"Erreur lors de l'ouverture du dossier: {ex.Message}");
                CustomMessageBox.Show(
                    $"Erreur lors de l'ouverture du dossier des plugins:\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(this);
                CaptureMouse();
                e.Handled = true;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TitleBar_MouseLeftButtonDown(sender, e);
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(this);
                Vector offset = currentPosition - _dragStartPoint;
                Left += offset.X;
                Top += offset.Y;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ThemeManager.AccentColorChanged -= ThemeManager_AccentColorChanged;
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// ViewModel pour afficher les informations d'un plugin local dans la liste
    /// </summary>
    public class PluginInfoViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsLoaded { get; set; }
        public string? ErrorMessage { get; set; }
        
        public string StatusText
        {
            get
            {
                if (!string.IsNullOrEmpty(ErrorMessage))
                    return "Erreur";
                if (IsLoaded && IsEnabled)
                    return "Actif";
                if (IsLoaded)
                    return "Chargé";
                return "Non chargé";
            }
        }
    }

    /// <summary>
    /// ViewModel pour afficher les informations d'un plugin disponible dans le dépôt
    /// </summary>
    public class PluginRepositoryViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string? InstalledVersion { get; set; }
        public bool HasUpdate { get; set; }
        
        public string StatusText
        {
            get
            {
                if (HasUpdate)
                    return "Mise à jour disponible";
                if (IsInstalled)
                    return "Installé";
                return "Disponible";
            }
        }
    }
}
