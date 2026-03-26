using GameOverlay.Models;
using GameOverlay.Themes;
using GameOverlay.Windows;
using CustomMessageBox = GameOverlay.Kikimeter.Views.CustomMessageBox;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using GameOverlay.Kikimeter.Views;
using GameOverlay.Kikimeter.Services;
using GameOverlay.Kikimeter.Models;
using GameOverlay.App.Services;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Threading;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfCursors = System.Windows.Input.Cursors;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace GameOverlay.App
{
    public partial class MainWindow : Window
    {
        private string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
        private bool isHidden = false;
        private Dictionary<string, WindowPosition> windowPositions = new Dictionary<string, WindowPosition>();
        private NotifyIcon notifyIcon;
        private ContextMenuStrip? mainWindowContextMenu;
        private Config config = new Config();
        private bool _isExplicitAppExitRequested = false;
        
        /// <summary>
        /// Obtient le menu contextuel Windows Forms du MainWindow
        /// </summary>
        public ContextMenuStrip? GetMainWindowContextMenu() => mainWindowContextMenu;
        
        /// <summary>
        /// Obtient la couleur de fond des bulles depuis la configuration
        /// </summary>
        public string GetBubbleBackgroundColor()
        {
            return config?.BubbleBackgroundColor ?? "#FF1A1A1A";
        }
        private bool wasKikimeterWindowVisible = false;
        
        // Kikimeter System
        private GameOverlay.Windows.KikimeterBubble? kikimeterBubble;
        private GameOverlay.Kikimeter.KikimeterWindow? kikimeterWindow;
        
        // Loot System
        // LootBubble n'est plus utilisée - elle est maintenant intégrée dans KikimeterBubble
        private GameOverlay.Windows.LootBubble? lootBubble;
        private GameOverlay.Kikimeter.Views.LootWindow? lootWindow;
        private GameOverlay.Kikimeter.Views.SettingsWindow? settingsWindow;
        
        // Web System
        private GameOverlay.Windows.WebWindow? webWindow;
        
        // Sale Notification System
        private GameOverlay.Kikimeter.Services.SaleTracker? _saleTracker;
        private readonly List<GameOverlay.Kikimeter.Views.SaleNotificationWindow> _saleNotificationWindows = new();
        private System.Windows.Threading.DispatcherTimer? _saleTrackerTimer;

        // Plugin System
        private GameOverlay.App.Services.PluginManager? _pluginManager;
        private PluginManagerWindow? _pluginManagerWindow;
        
        // Interactive Map Window
        private InteractiveMapWindow? _interactiveMapWindow;

        private int _openContextMenus;
        private bool _focusReturnPending;
        private string _lastDetectedServerName = string.Empty;

        public MainWindow()
        {
            try
            {
                Logger.Info("MainWindow", "Initialisation de MainWindow");
                
                InitializeComponent();
                
                // Initialiser le menu contextuel Windows Forms (comme le NotifyIcon)
                InitializeMainWindowContextMenu();

                // Optimisations Windows 11
                OptimizeForWindows11();

                // Met la fenêtre en plein écran virtuel (tous les écrans)
                this.Left = SystemParameters.VirtualScreenLeft;
                this.Top = SystemParameters.VirtualScreenTop;
                this.Width = SystemParameters.VirtualScreenWidth;
                this.Height = SystemParameters.VirtualScreenHeight;

                // Initialiser le NotifyIcon
                InitializeNotifyIcon();

                // Initialiser les bulles APRÈS le rendu du MainCanvas
                this.Loaded += (s, e) => {
                    try
                    {
                        Logger.Info("MainWindow", "Loaded event déclenché");
                        // Appliquer le thème au menu contextuel
                        if (ContextMenu != null)
                        {
                            ThemeManager.ApplyContextMenuTheme(ContextMenu);
                            // Forcer le thème à chaque ouverture pour éviter le cyan
                            ContextMenu.Opened += (s, e) =>
                            {
                                if (s is System.Windows.Controls.ContextMenu menu)
                                {
                                    ThemeManager.ApplyContextMenuTheme(menu);
                                }
                            };
                        }
                        LoadWindowPositionsFromFile();
                        bool pathsWereUpdated = LoadConfiguration();
                        
                        // Message de bienvenue désactivé (demandé par l'utilisateur)
                        // CheckAndShowWelcomeMessage();
                        
                        // Créer la bulle Kikimeter si elle n'existe pas déjà
                        // Cette partie était après un return dans LoadConfiguration() et n'était jamais exécutée
                        if (kikimeterBubble == null)
                        {
                            double centerX = SystemParameters.PrimaryScreenWidth / 2;
                            double centerY = SystemParameters.PrimaryScreenHeight / 2;
                            CreateKikimeterBubble((int)centerX, (int)centerY + 100);
                            Logger.Info("MainWindow", "Bulle Kikimeter créée au démarrage");
                        }
                        
                        // Ne plus créer LootBubble séparée, elle est maintenant intégrée dans KikimeterBubble
                        // Cacher ou supprimer LootBubble si elle existe
                        if (lootBubble != null)
                        {
                            try
                            {
                                MainCanvas.Children.Remove(lootBubble);
                                lootBubble = null;
                                // Réinitialiser la position sauvegardée
                                config.LootBubbleX = -1;
                                config.LootBubbleY = -1;
                                SaveConfiguration();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("MainWindow", $"Erreur lors de la suppression de LootBubble: {ex.Message}");
                            }
                        }
                        
                        // Créer les fenêtres au démarrage pour démarrer la surveillance même si elles ne sont pas visibles
                        InitializeWindowsInBackground();
                        
                        // Si les chemins ont été mis à jour, redémarrer les watchers avec les nouveaux chemins
                        if (pathsWereUpdated)
                        {
                            Logger.Info("MainWindow", "Les chemins de log ont été mis à jour, redémarrage des watchers...");
                            RestartWatchersWithNewPaths();
                        }
                        
                        // S'assurer que la bulle Kikimeter est visible après l'initialisation
                        // Les fenêtres KikimeterWindow et LootWindow doivent rester cachées par défaut
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (kikimeterBubble != null && kikimeterBubble.Visibility != Visibility.Visible)
                                {
                                    Logger.Info("MainWindow", "Affichage de la bulle Kikimeter au démarrage");
                                    kikimeterBubble.Visibility = Visibility.Visible;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("MainWindow", $"Erreur lors de l'affichage de la bulle au démarrage: {ex.Message}");
                            }
                        }), DispatcherPriority.Loaded);
                        
                        // Initialiser le SaleTracker après le chargement de la configuration
                        // Ne pas vérifier File.Exists - le SaleTracker surveille même si le fichier n'existe pas encore
                        if (!string.IsNullOrEmpty(config.LootChatLogPath))
                        {
                            InitializeSaleTracker();
                        }
                        
                        // Initialiser le PluginManager
                        InitializePluginManager();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", "Erreur dans l'événement Loaded: " + ex.Message);
                    }
                };
                
                // Exclure l'overlay d'Alt+Tab
                this.SourceInitialized += MainWindow_SourceInitialized;

                // Nettoyer les ressources à la fermeture
                this.Closing += MainWindow_Closing;
                this.Closed += (s, e) => {
                    try
                    {
                        Logger.Info("MainWindow", "Closed event déclenché");
                        
                        // Libérer le SaleTracker
                        if (_saleTracker != null)
                        {
                            _saleTracker.SaleDetected -= SaleTracker_SaleDetected;
                            _saleTracker.Dispose();
                            _saleTracker = null;
                        }
                        
                        // Arrêter le timer du SaleTracker
                        if (_saleTrackerTimer != null)
                        {
                            _saleTrackerTimer.Stop();
                            _saleTrackerTimer.Tick -= SaleTrackerTimer_Tick;
                            _saleTrackerTimer = null;
                        }
                        
                        // Nettoyer le NotifyIcon
                        if (notifyIcon != null)
                        {
                            notifyIcon.Visible = false;
                            notifyIcon.Dispose();
                            notifyIcon = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", "Erreur dans l'événement Closed: " + ex.Message);
                    }
                };
                
                Logger.Info("MainWindow", "MainWindow initialisé avec succès");
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Erreur lors de l'initialisation de MainWindow: " + ex.Message);
                throw;
            }
        }

        // Méthodes ZQSD supprimées - fonctionnalité retirée

        private void RecreateKikimeterBubble()
        {
            try
            {
                if (kikimeterBubble != null)
                {
                    MainCanvas.Children.Remove(kikimeterBubble);
                    kikimeterBubble = null;
                }
                
                config.KikimeterBubbleX = -1;
                config.KikimeterBubbleY = -1;
                
                double centerX = SystemParameters.PrimaryScreenWidth / 2;
                double centerY = SystemParameters.PrimaryScreenHeight / 2;
                CreateKikimeterBubble((int)centerX, (int)centerY + 245);
                
                CustomMessageBox.Show("Bulle Kikimeter recréée au centre.", "Information", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur: {ex.Message}", "Erreur", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecreateLootBubble()
        {
            try
            {
                if (lootBubble != null)
                {
                    MainCanvas.Children.Remove(lootBubble);
                    lootBubble = null;
                }
                
                config.LootBubbleX = -1;
                config.LootBubbleY = -1;
                
                double centerX = SystemParameters.PrimaryScreenWidth / 2;
                double centerY = SystemParameters.PrimaryScreenHeight / 2;
                CreateLootBubble((int)centerX, (int)centerY + 315);
                
                CustomMessageBox.Show("Bulle Loot recréée au centre.", "Information", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur: {ex.Message}", "Erreur", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Méthodes Music/ZQSD supprimées - fonctionnalités retirées

        private void InitializeNotifyIcon()
        {
            try
            {
                // Créer le NotifyIcon
                notifyIcon = new NotifyIcon();
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Amalia.ico");
                if (File.Exists(iconPath))
                {
                    notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                }
                notifyIcon.Text = "Amaliassistant - Overlay de sites web";
                notifyIcon.Visible = true;

                // Créer le menu contextuel avec le style PluginManagerWindow - utiliser l'image de fond
                var contextMenu = new ContextMenuStrip();
                contextMenu.Renderer = new DarkMenuRenderer();
                
                // Charger l'image EndTurnWidgetBackground.png pour le fond depuis les ressources WPF
                try
                {
                    // Utiliser WPF pour charger l'image depuis les ressources pack://
                    var uri = new Uri("pack://application:,,,/EndTurnWidgetBackground.png");
                    var streamResourceInfo = System.Windows.Application.GetResourceStream(uri);
                    if (streamResourceInfo != null)
                    {
                        using (var stream = streamResourceInfo.Stream)
                        {
                            var bitmap = new System.Drawing.Bitmap(stream);
                            contextMenu.BackgroundImage = bitmap;
                            contextMenu.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
                        }
                    }
                }
                catch
                {
                    // Si l'image ne peut pas être chargée, utiliser une couleur de fallback
                    contextMenu.BackColor = System.Drawing.Color.FromArgb(255, 222, 203, 161);
                }
                
                // Texte plus clair pour être visible : #FF6E5C2A (RGB: 110, 92, 42)
                contextMenu.ForeColor = System.Drawing.Color.FromArgb(255, 110, 92, 42);
                
                // Forcer les couleurs de tous les items pour éviter le cyan
                System.Drawing.Color textColor = System.Drawing.Color.FromArgb(255, 110, 92, 42);
                System.Drawing.Color hoverColor = System.Drawing.Color.FromArgb(150, 110, 92, 42);
                
                var kikimeterItem = new ToolStripMenuItem("📊 Ouvrir le Kikimeter");
                kikimeterItem.ForeColor = textColor;
                kikimeterItem.Click += (s, e) => ToggleKikimeter();
                contextMenu.Items.Add(kikimeterItem);

                var lootItem = new ToolStripMenuItem("💎 Ouvrir le Loot");
                lootItem.ForeColor = textColor;
                lootItem.Click += (s, e) => ToggleLoot();
                contextMenu.Items.Add(lootItem);
                
                var mapItem = new ToolStripMenuItem("🗺️ Carte interactive Wakfu");
                mapItem.ForeColor = textColor;
                mapItem.Click += (s, e) => ToggleInteractiveMap();
                contextMenu.Items.Add(mapItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                var settingsItem = new ToolStripMenuItem("⚙️ Paramètres");
                settingsItem.ForeColor = textColor;
                settingsItem.Click += (s, e) => ToggleSettingsWindow();
                contextMenu.Items.Add(settingsItem);

                var pluginsItem = new ToolStripMenuItem("🔌 Plugins");
                pluginsItem.ForeColor = textColor;
                pluginsItem.Click += (s, e) => TogglePluginManagerWindow();
                contextMenu.Items.Add(pluginsItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Option de lancement automatique
                var startupItem = new ToolStripMenuItem("🚀 Lancer au démarrage");
                startupItem.ForeColor = textColor;
                startupItem.CheckOnClick = true;
                startupItem.Checked = IsStartupEnabled();
                startupItem.Click += (s, e) => ToggleStartup();
                contextMenu.Items.Add(startupItem);

                // Option de vérification des mises à jour
                var updateItem = new ToolStripMenuItem("🔄 Vérifier les mises à jour");
                updateItem.ForeColor = textColor;
                updateItem.Click += (s, e) => CheckForUpdatesManually();
                contextMenu.Items.Add(updateItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                var exitItem = new ToolStripMenuItem("❌ Quitter");
                exitItem.ForeColor = textColor;
                exitItem.Click += (s, e) => ExitApplication();
                contextMenu.Items.Add(exitItem);

                notifyIcon.ContextMenuStrip = contextMenu;

                // Gérer le double-clic pour afficher/masquer
                notifyIcon.DoubleClick += (s, e) => ToggleOverlay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur initialisation NotifyIcon: {ex.Message}");
            }
        }

        private void InitializeMainWindowContextMenu()
        {
            try
            {
                // Créer le menu contextuel avec le même style que le NotifyIcon
                var contextMenu = new ContextMenuStrip();
                contextMenu.Renderer = new DarkMenuRenderer();
                
                // Charger l'image EndTurnWidgetBackground.png pour le fond
                try
                {
                    var uri = new Uri("pack://application:,,,/EndTurnWidgetBackground.png");
                    var streamResourceInfo = System.Windows.Application.GetResourceStream(uri);
                    if (streamResourceInfo != null)
                    {
                        using (var stream = streamResourceInfo.Stream)
                        {
                            var bitmap = new System.Drawing.Bitmap(stream);
                            contextMenu.BackgroundImage = bitmap;
                            contextMenu.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
                        }
                    }
                }
                catch
                {
                    // Si l'image ne peut pas être chargée, utiliser une couleur de fallback
                    contextMenu.BackColor = System.Drawing.Color.FromArgb(255, 222, 203, 161);
                }
                
                // Texte : #FF6E5C2A (RGB: 110, 92, 42)
                contextMenu.ForeColor = System.Drawing.Color.FromArgb(255, 110, 92, 42);
                
                // S'abonner à l'événement Opening pour mettre à jour les plugins à chaque ouverture
                contextMenu.Opening += (s, e) => UpdateContextMenuWithPlugins(contextMenu);
                
                mainWindowContextMenu = contextMenu;
                
                // Construire le menu initial
                BuildContextMenuItems(contextMenu);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur initialisation MainWindowContextMenu: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Construit les items de base du menu contextuel
        /// </summary>
        private void BuildContextMenuItems(ContextMenuStrip contextMenu)
        {
            // Couleur du texte
            System.Drawing.Color textColor = System.Drawing.Color.FromArgb(255, 110, 92, 42);
            
            // Nettoyer les items existants (sauf les plugins qui seront ajoutés dynamiquement)
            var itemsToRemove = new List<ToolStripItem>();
            foreach (ToolStripItem item in contextMenu.Items)
            {
                // Garder les séparateurs et les plugins (tag "Plugin")
                if (item.Tag?.ToString() != "Plugin")
                {
                    itemsToRemove.Add(item);
                }
            }
            foreach (var item in itemsToRemove)
            {
                contextMenu.Items.Remove(item);
            }
            
            // Trouver l'index où insérer les items (après les plugins)
            int insertIndex = 0;
            for (int i = 0; i < contextMenu.Items.Count; i++)
            {
                if (contextMenu.Items[i].Tag?.ToString() == "Plugin")
                {
                    insertIndex = i + 1;
                }
            }
            
            // Item "Masquer l'overlay"
            var hideItem = new ToolStripMenuItem("Masquer l'overlay");
            hideItem.ForeColor = textColor;
            hideItem.Click += (s, e) => HideOverlay_Click(s, e);
            contextMenu.Items.Insert(insertIndex++, hideItem);
            
            contextMenu.Items.Insert(insertIndex++, new ToolStripSeparator());
            
            // Item "Placer notification de vente"
            var saleItem = new ToolStripMenuItem("📍 Placer notification de vente");
            saleItem.ForeColor = textColor;
            saleItem.Click += (s, e) => TestSaleNotification_Click(s, e);
            contextMenu.Items.Insert(insertIndex++, saleItem);
            
            contextMenu.Items.Insert(insertIndex++, new ToolStripSeparator());
            
            // Item "Plugins"
            var pluginsItem = new ToolStripMenuItem("🔌 Plugins");
            pluginsItem.ForeColor = textColor;
            pluginsItem.Click += (s, e) => TogglePluginManagerWindow();
            contextMenu.Items.Insert(insertIndex++, pluginsItem);
            
            contextMenu.Items.Insert(insertIndex++, new ToolStripSeparator());
            
            // Item "Quitter"
            var exitItem = new ToolStripMenuItem("Quitter");
            exitItem.ForeColor = textColor;
            exitItem.Click += (s, e) => Exit_Click(s, e);
            contextMenu.Items.Insert(insertIndex++, exitItem);
        }
        
        /// <summary>
        /// Met à jour le menu contextuel avec les plugins activés
        /// </summary>
        private void UpdateContextMenuWithPlugins(ContextMenuStrip contextMenu)
        {
            try
            {
                // S'assurer que le PluginManager est initialisé
                if (_pluginManager == null)
                {
                    InitializePluginManager();
                }
                
                if (_pluginManager == null)
                {
                    return;
                }
                
                // Couleur du texte
                System.Drawing.Color textColor = System.Drawing.Color.FromArgb(255, 110, 92, 42);
                
                // Retirer tous les items de plugins existants
                var pluginItemsToRemove = new List<ToolStripItem>();
                foreach (ToolStripItem item in contextMenu.Items)
                {
                    if (item.Tag?.ToString() == "Plugin")
                    {
                        pluginItemsToRemove.Add(item);
                    }
                }
                foreach (var item in pluginItemsToRemove)
                {
                    contextMenu.Items.Remove(item);
                }
                
                // Les séparateurs associés aux plugins seront aussi retirés avec les items de plugins
                
                // Obtenir tous les plugins activés
                var enabledPlugins = _pluginManager.GetAllPlugins()
                    .Where(p => p.IsEnabled)
                    .OrderBy(p => p.Name)
                    .ToList();
                
                if (enabledPlugins.Count > 0)
                {
                    // Trouver l'index de l'item "🔌 Plugins" (gestionnaire de plugins)
                    int pluginsManagerIndex = -1;
                    for (int i = 0; i < contextMenu.Items.Count; i++)
                    {
                        var item = contextMenu.Items[i];
                        if (item is ToolStripMenuItem menuItem && menuItem.Text == "🔌 Plugins")
                        {
                            pluginsManagerIndex = i;
                            break;
                        }
                    }
                    
                    // Si on n'a pas trouvé l'item, insérer avant le dernier séparateur (avant "Quitter")
                    if (pluginsManagerIndex == -1)
                    {
                        for (int i = contextMenu.Items.Count - 1; i >= 0; i--)
                        {
                            if (contextMenu.Items[i] is ToolStripSeparator)
                            {
                                pluginsManagerIndex = i;
                                break;
                            }
                        }
                    }
                    
                    // Si toujours pas trouvé, insérer à la fin
                    if (pluginsManagerIndex == -1)
                    {
                        pluginsManagerIndex = contextMenu.Items.Count;
                    }
                    
                    // Ajouter un séparateur avant les plugins si nécessaire
                    int insertIndex = pluginsManagerIndex;
                    if (insertIndex > 0 && !(contextMenu.Items[insertIndex - 1] is ToolStripSeparator))
                    {
                        var separatorBefore = new ToolStripSeparator();
                        separatorBefore.Tag = "Plugin";
                        contextMenu.Items.Insert(insertIndex++, separatorBefore);
                    }
                    
                    // Ajouter chaque plugin activé
                    foreach (var pluginInfo in enabledPlugins)
                    {
                        // Obtenir le plugin chargé pour vérifier son état
                        var plugin = _pluginManager.GetPlugin(pluginInfo.Id);
                        
                        // Si le plugin n'est pas chargé, essayer de le charger
                        if (plugin == null)
                        {
                            try
                            {
                                if (_pluginManager.GetPluginInfo(pluginInfo.Id) != null)
                                {
                                    _pluginManager.EnablePlugin(pluginInfo.Id);
                                    plugin = _pluginManager.GetPlugin(pluginInfo.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("MainWindow", $"Erreur lors du chargement du plugin {pluginInfo.Name}: {ex.Message}");
                            }
                        }
                        
                        // Créer l'item de menu avec indication visuelle si le plugin est actif
                        bool isActive = plugin?.IsActive ?? false;
                        string menuText = isActive ? $"✓ {pluginInfo.Name}" : pluginInfo.Name;
                        var pluginItem = new ToolStripMenuItem(menuText);
                        pluginItem.ForeColor = textColor;
                        pluginItem.Tag = "Plugin";
                        pluginItem.Checked = isActive; // Ajouter une coche si actif
                        
                        if (plugin != null)
                        {
                            // Capturer les variables pour le closure
                            var pluginId = pluginInfo.Id;
                            var pluginName = pluginInfo.Name;
                            
                            pluginItem.Click += (s, e) =>
                            {
                                try
                                {
                                    // Toggle : si actif, désactiver, sinon activer
                                    var currentPlugin = _pluginManager.GetPlugin(pluginId);
                                    if (currentPlugin != null)
                                    {
                                        if (currentPlugin.IsActive)
                                        {
                                            currentPlugin.Deactivate();
                                            Logger.Info("MainWindow", $"Plugin {pluginName} désactivé via le menu contextuel");
                                        }
                                        else
                                        {
                                            currentPlugin.Activate();
                                            Logger.Info("MainWindow", $"Plugin {pluginName} activé via le menu contextuel");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("MainWindow", $"Erreur lors du toggle du plugin {pluginName}: {ex.Message}");
                                }
                            };
                        }
                        else
                        {
                            // Si le plugin n'est toujours pas chargé après tentative, désactiver l'item
                            pluginItem.Enabled = false;
                        }
                        
                        contextMenu.Items.Insert(insertIndex++, pluginItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la mise à jour du menu contextuel avec les plugins: {ex.Message}");
            }
        }

        // Renderer personnalisé pour le menu sombre
        private class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColorTable()) { }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                // Effets de survol désactivés pour meilleure lisibilité
                // Le fond sera géré par l'image de fond du menu lui-même (BackgroundImage)
                // Ne rien dessiner pour éviter tout effet de survol
            }
            

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                // Séparateur transparent pour correspondre au style PluginManagerWindow
                // Pas de ligne visible, juste de l'espace
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                // Rendre la marge d'image transparente pour éviter le bandeau beige
                // On ne dessine rien, ce qui permet à l'image de fond du menu de s'afficher
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                // Dessiner la bordure brune au lieu de la couleur par défaut
                var borderColor = System.Drawing.Color.FromArgb(255, 110, 92, 42); // #FF6E5C2A
                using (var pen = new System.Drawing.Pen(borderColor))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                }
            }
        }

            // Table de couleurs personnalisée - exactement les mêmes couleurs que le menu WPF
        private class DarkColorTable : ProfessionalColorTable
        {
            // Contour : #FF6E5C2A (RGB: 110, 92, 42) - exactement comme le menu WPF
            private static readonly System.Drawing.Color BorderColor = System.Drawing.Color.FromArgb(255, 110, 92, 42);
            // Fond : beige/jaune-marron comme l'image EndTurnWidgetBackground.png (utilisé comme fallback si l'image ne charge pas)
            private static readonly System.Drawing.Color BackgroundColor = System.Drawing.Color.FromArgb(255, 222, 203, 161);
            // Effets de survol désactivés - toutes les couleurs de survol sont transparentes
            private static readonly System.Drawing.Color TransparentColor = System.Drawing.Color.Transparent;
            
            public override System.Drawing.Color MenuBorder => BorderColor;
            public override System.Drawing.Color MenuItemBorder => BorderColor;
            
            // Toutes les couleurs de survol sont transparentes pour retirer les effets
            public override System.Drawing.Color MenuItemSelected => TransparentColor;
            public override System.Drawing.Color MenuItemSelectedGradientBegin => TransparentColor;
            public override System.Drawing.Color MenuItemSelectedGradientEnd => TransparentColor;
            public override System.Drawing.Color MenuItemPressedGradientBegin => TransparentColor;
            public override System.Drawing.Color MenuItemPressedGradientEnd => TransparentColor;
            // Checked aussi transparent
            public override System.Drawing.Color CheckBackground => TransparentColor;
            public override System.Drawing.Color CheckPressedBackground => TransparentColor;
            public override System.Drawing.Color CheckSelectedBackground => TransparentColor;
            // Button
            public override System.Drawing.Color ButtonSelectedHighlight => TransparentColor;
            public override System.Drawing.Color ButtonSelectedHighlightBorder => BorderColor;
            public override System.Drawing.Color ButtonPressedHighlight => TransparentColor;
            public override System.Drawing.Color ButtonPressedHighlightBorder => BorderColor;
            // Separator - transparent
            public override System.Drawing.Color SeparatorDark => System.Drawing.Color.Transparent;
            public override System.Drawing.Color SeparatorLight => System.Drawing.Color.Transparent;
            // Grip
            public override System.Drawing.Color GripLight => System.Drawing.Color.Transparent;
            public override System.Drawing.Color GripDark => System.Drawing.Color.Transparent;
            // Le fond sera géré par BackgroundImage du ContextMenuStrip, cette couleur est un fallback
            public override System.Drawing.Color ToolStripDropDownBackground => BackgroundColor;
            // Rendre la marge d'image transparente pour éviter le bandeau beige dégueulasse
            // Utiliser Transparent pour que l'image de fond du menu s'affiche
            public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.Transparent;
            public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.Transparent;
            public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.Transparent;
        }


        private void ShowOverlay()
        {
            // Afficher la bulle Kikimeter
            if (kikimeterBubble != null)
            {
                kikimeterBubble.Visibility = Visibility.Visible;
            }
            // Afficher la bulle Loot
            // LootBubble n'est plus utilisée - elle est maintenant intégrée dans KikimeterBubble
            // if (lootBubble != null)
            // {
            //     lootBubble.Visibility = Visibility.Visible;
            // }
            // Restaurer l'état des fenêtres si elles étaient visibles
            if (kikimeterWindow != null && wasKikimeterWindowVisible && !kikimeterWindow.UserRequestedHidden)
            {
                kikimeterWindow.ShowFromController(false, resetUserFlag: false);
            }
            
            isHidden = false;
        }

        private void HideOverlay()
        {
            // Masquer la bulle Kikimeter
            if (kikimeterBubble != null)
            {
                kikimeterBubble.Visibility = Visibility.Hidden;
            }
            // Masquer la bulle Loot
            // LootBubble n'est plus utilisée - elle est maintenant intégrée dans KikimeterBubble
            // if (lootBubble != null)
            // {
            //     lootBubble.Visibility = Visibility.Hidden;
            // }
            // Mémoriser et masquer les fenêtres détachées
            wasKikimeterWindowVisible = kikimeterWindow != null && kikimeterWindow.IsVisible;
            if (kikimeterWindow != null && kikimeterWindow.IsVisible) kikimeterWindow.HideFromController(false);
            
            isHidden = true;
        }

        public void ToggleOverlay()
        {
            if (isHidden)
            {
                ShowOverlay();
            }
            else
            {
                HideOverlay();
            }
        }

        private void ExitApplication()
        {
            _isExplicitAppExitRequested = true;
            CleanupNotifyIcon();
            System.Windows.Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Empêcher les fermetures involontaires:
                // - autoriser si l'utilisateur a explicitement demandé "Quitter"
                // - autoriser pendant une mise à jour (UpdateService ferme les fenêtres)
                if (_isExplicitAppExitRequested || UpdateService.IsUpdating)
                {
                    Logger.Info("MainWindow", $"Fermeture autorisée (explicitExit={_isExplicitAppExitRequested}, isUpdating={UpdateService.IsUpdating})");
                    return;
                }

                // Dans tous les autres cas, annuler la fermeture pour éviter un arrêt inattendu
                e.Cancel = true;
                Logger.Error("MainWindow", "Demande de fermeture inattendue interceptée et annulée.");
                Logger.Error("MainWindow", $"Stack de fermeture inattendue: {Environment.StackTrace}");

                // Conserver le comportement en arrière-plan
                this.Hide();
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur dans MainWindow_Closing: {ex.Message}");
            }
        }

        /// <summary>
        /// Nettoie le NotifyIcon (méthode publique pour être appelée depuis UpdateService)
        /// </summary>
        public void CleanupNotifyIcon()
        {
            try
            {
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                    notifyIcon = null;
                    Logger.Info("MainWindow", "NotifyIcon nettoyé");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors du nettoyage du NotifyIcon: {ex.Message}");
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("Amaliassistant") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void ToggleStartup()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (IsStartupEnabled())
                    {
                        key?.DeleteValue("Amaliassistant", false);
                    }
                    else
                    {
                        key?.SetValue("Amaliassistant", System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur toggle startup: {ex.Message}");
            }
        }

        private void CheckForUpdatesManually()
        {
            try
            {
                Logger.Info("MainWindow", "Vérification manuelle des mises à jour demandée par l'utilisateur");
                GameOverlay.App.Services.UpdateService.CheckForUpdateAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la vérification manuelle des mises à jour: {ex.Message}");
                CustomMessageBox.Show(
                    $"Erreur lors de la vérification des mises à jour:\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
            // Exclure l'overlay d'Alt+Tab
            ExcludeFromAltTab(helper.Handle);
        }
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        
        private void ExcludeFromAltTab(IntPtr hwnd)
        {
            try
            {
                // Récupérer le style étendu actuel
                uint extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                // Ajouter WS_EX_TOOLWINDOW pour exclure de Alt+Tab
                extendedStyle |= WS_EX_TOOLWINDOW;
                // Appliquer le nouveau style et vérifier le résultat
                int result = SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
                if (result == 0)
                {
                    // Vérifier s'il y a eu une erreur
                    int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    if (error != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erreur SetWindowLong: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur ExcludeFromAltTab: {ex.Message}");
            }
        }

        private void OptimizeForWindows11()
        {
            try
            {
                // Optimisations pour Windows 11
                
                // 1. Configurer la fenêtre pour Windows 11
                this.AllowsTransparency = true;
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.Topmost = true;
                this.ShowInTaskbar = false;
                
                // 2. Optimiser la gestion DPI pour Windows 11
                this.UseLayoutRounding = true;
                this.SnapsToDevicePixels = true;
                
                // 3. Configurer les options de performance
                this.Background = WpfBrushes.Transparent;
                
                System.Diagnostics.Debug.WriteLine("Optimisations Windows 11 appliquées");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur optimisations Windows 11: {ex.Message}");
            }
        }

        public void ApplyLightStyleToMenuItem(WpfMenuItem item)
        {
            item.Background = new SolidColorBrush(Colors.White);
            item.Foreground = new SolidColorBrush(Colors.Black);
        }

        public WpfMenuItem CreateLightMenuItem(string header, double value)
        {
            var item = new WpfMenuItem { Header = header, Tag = value };
            ApplyLightStyleToMenuItem(item);
            return item;
        }

        /// <summary>
        /// Obtient la position de la souris en coordonnées Canvas pour le support multi-écrans
        /// Utilise la position de la souris par rapport à la fenêtre principale
        /// </summary>
        public WpfPoint GetMouseCanvasPosition()
        {
            // Mouse.GetPosition(this) donne les coordonnées par rapport à cette fenêtre
            // Comme le Canvas remplit la fenêtre, ces coordonnées correspondent déjà au Canvas
            return Mouse.GetPosition(this);
        }

        // Méthode supprimée : RemoveBubble - fonctionnalité sites web retirée

        private bool LoadConfiguration()
        {
            try
            {
                Logger.Debug("MainWindow", "Chargement de la configuration");
                
                // Vider wakfu_chat.log pour commencer une nouvelle session
                try
                {
                    string? wakfuLogPath = GameOverlay.Kikimeter.Services.WakfuLogFinder.FindFirstLogFile();
                    if (!string.IsNullOrEmpty(wakfuLogPath))
                    {
                        string chatLogPath = GameOverlay.Kikimeter.Services.WakfuLogFinder.FindChatLogFile(wakfuLogPath);
                        if (!string.IsNullOrEmpty(chatLogPath) && File.Exists(chatLogPath))
                        {
                            File.WriteAllText(chatLogPath, string.Empty);
                            Logger.Debug("MainWindow", $"✓ wakfu_chat.log vidé pour nouvelle session: {chatLogPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    GameOverlay.Models.Logger.Warning("MainWindow", "Erreur lors du vidage de wakfu_chat.log: " + ex.Message);
                }
                
                if (File.Exists(configFile))
                {
                    try
                    {
                        string json = File.ReadAllText(configFile);
                        
                        // Créer une sauvegarde de la configuration existante avant de la charger
                        // Cela permet de récupérer les paramètres en cas d'erreur de désérialisation
                        string backupConfigFile = configFile + ".backup";
                        try
                        {
                            File.Copy(configFile, backupConfigFile, overwrite: true);
                        }
                        catch
                        {
                            // Ignorer les erreurs de sauvegarde, ce n'est pas critique
                        }
                        
                        // Désérialiser la configuration
                        var loadedConfig = JsonConvert.DeserializeObject<Config>(json);
                        
                        if (loadedConfig != null)
                        {
                            config = loadedConfig;
                            Logger.Info("MainWindow", "Configuration chargée avec succès depuis config.json");
                        }
                        else
                        {
                            // Si la désérialisation échoue, créer une nouvelle config mais préserver les valeurs importantes
                            GameOverlay.Models.Logger.Warning("MainWindow", "Échec de la désérialisation, utilisation de la configuration par défaut");
                            config = new Config();
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        // Fichier JSON corrompu, essayer de récupérer depuis la sauvegarde
                        Logger.Error("MainWindow", $"Erreur de désérialisation JSON: {jsonEx.Message}");
                        string backupConfigFile = configFile + ".backup";
                        if (File.Exists(backupConfigFile))
                        {
                            try
                            {
                                string backupJson = File.ReadAllText(backupConfigFile);
                                var backupConfig = JsonConvert.DeserializeObject<Config>(backupJson);
                                if (backupConfig != null)
                                {
                                    config = backupConfig;
                                    Logger.Info("MainWindow", "Configuration restaurée depuis la sauvegarde");
                                    // Restaurer la sauvegarde comme fichier principal
                                    File.Copy(backupConfigFile, configFile, overwrite: true);
                                }
                                else
                                {
                                    config = new Config();
                                }
                            }
                            catch
                            {
                                config = new Config();
                            }
                        }
                        else
                        {
                            config = new Config();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors du chargement de la configuration: {ex.Message}");
                        config = new Config();
                    }

                    // ZQSDConfigurations supprimé - fonctionnalité ZQSD retirée

                    // Charger la couleur d'accent sauvegardée
                    if (!string.IsNullOrEmpty(config.AccentColorHex))
                    {
                        try
                        {
                            ThemeManager.SetAccentColorFromHex(config.AccentColorHex);
                            Logger.Debug("MainWindow", $"Couleur d'accent chargée: {config.AccentColorHex}");
                        }
                        catch (Exception ex)
                        {
                            GameOverlay.Models.Logger.Warning("MainWindow", "Erreur chargement couleur d'accent: " + ex.Message);
                        }
                    }
                    
                    // Charger la couleur de fond des bulles sauvegardée
                    if (!string.IsNullOrEmpty(config.BubbleBackgroundColor))
                    {
                        try
                        {
                            ThemeManager.BubbleBackgroundColor = config.BubbleBackgroundColor;
                            Logger.Debug("MainWindow", $"Couleur de fond des bulles chargée: {config.BubbleBackgroundColor}");
                        }
                        catch (Exception ex)
                        {
                            GameOverlay.Models.Logger.Warning("MainWindow", "Erreur chargement couleur de fond des bulles: " + ex.Message);
                        }
                    }

                    // Valider et corriger automatiquement les chemins de log
                    bool pathsUpdated = ValidateAndFixLogPaths();
                    return pathsUpdated;

                }
                else
                {
                    config = new Config();
                    Logger.Info("MainWindow", "Fichier de configuration non trouvé, utilisation de la configuration par défaut");
                    
                    // Essayer de trouver automatiquement les chemins de log
                    bool pathsUpdated = ValidateAndFixLogPaths();
                    return pathsUpdated;
                }
                
                // NOTE: La création de la bulle Kikimeter a été déplacée dans le Loaded event handler
                // pour être exécutée après LoadConfiguration()
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur chargement config: {ex.Message}");
                config = new Config();
                return false;
            }
        }

        // Méthode supprimée : CreateAddSiteBubble - fonctionnalité sites web retirée (la bulle engrenage n'est plus nécessaire)
        // Méthode supprimée : CreateDefaultBubbles - fonctionnalité sites web retirée, plus utilisée

        // Méthodes supprimées : CreateMusicBubble, SaveMusicBubbleSettings - fonctionnalité musique retirée

        // Méthodes supprimées : CreateVideoBubble, SaveVideoBubbleSettings, RemoveVideoBubble, RecreateVideoBubble, ToggleVideoBubble, ShowVideoPlayer - fonctionnalité vidéo retirée

        private void ToggleKikimeterBubble()
        {
            try
            {
                if (kikimeterBubble == null)
                {
                    double cx = SystemParameters.PrimaryScreenWidth / 2;
                    double cy = SystemParameters.PrimaryScreenHeight / 2;
                    CreateKikimeterBubble((int)cx, (int)cy);
                }
                else
                {
                    RemoveKikimeterBubble();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Erreur ToggleKikimeterBubble: " + ex.Message);
            }
        }

        // LootBubble n'est plus utilisée - elle est maintenant intégrée dans KikimeterBubble
        /*
        private void ToggleLootBubble()
        {
            try
            {
                if (lootBubble == null)
                {
                    double cx = SystemParameters.PrimaryScreenWidth / 2;
                    double cy = SystemParameters.PrimaryScreenHeight / 2;
                    CreateLootBubble((int)cx, (int)cy);
                }
                else
                {
                    RemoveLootBubble();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Erreur ToggleLootBubble: " + ex.Message);
            }
        }
        */

        // Méthode RecreateMusicBubble déjà existante plus haut – ne pas dupliquer

        // Méthode supprimée : ShowVideoPlayer - fonctionnalité vidéo retirée

        // Méthode supprimée : CreateZQSDBubble - fonctionnalité ZQSD retirée

        private void UpdateSettingsBubbleSize(Border bubble, TextBlock icon, double newSize)
        {
            bubble.Width = newSize;
            bubble.Height = newSize;
            bubble.CornerRadius = new CornerRadius(newSize / 2);
            icon.FontSize = newSize * 0.48;
                // SettingsBubbleSize supprimé - fonctionnalité sites web retirée
            SaveConfiguration();
        }

        private void UpdateSettingsBubbleOpacity(Border bubble, double newOpacity)
        {
            bubble.Opacity = newOpacity;
            // Recalculer le fond avec la couleur de config
            try
            {
                var bgColor = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(GetBubbleBackgroundColor());
                byte alpha = (byte)(bgColor.A * newOpacity);
                var bgWithOpacity = WpfColor.FromArgb(alpha, bgColor.R, bgColor.G, bgColor.B);
                bubble.Background = new SolidColorBrush(bgWithOpacity);
            }
            catch { }
            // SettingsBubbleOpacity supprimé - fonctionnalité sites web retirée
            SaveConfiguration();
        }

        // Méthodes supprimées : EnsureMusicPlayerCreated, ShowMusicPlayer, RemoveMusicBubble - fonctionnalité musique retirée

        private void CreateKikimeterBubble(int x, int y)
        {
            try
            {
                Logger.Debug("MainWindow", $"CreateKikimeterBubble appelé pour ({x}, {y})");
                
                // Supprimer l'ancienne bulle si elle existe
                if (kikimeterBubble != null)
                {
                    Logger.Debug("MainWindow", "Suppression de l'ancienne KikimeterBubble");
                    MainCanvas.Children.Remove(kikimeterBubble);
                    kikimeterBubble = null;
                }
                // Charger les paramètres de personnalisation depuis la config
                double size = config.KikimeterBubbleSize;
                double opacity = config.KikimeterBubbleOpacity;
                
                // Charger la position sauvegardée si elle existe
                int posX = config.KikimeterBubbleX;
                int posY = config.KikimeterBubbleY;
                
                // Si position invalide ou hors écran, utiliser la valeur par défaut
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                
                if (posX == -1 || posX < 0 || posX > screenWidth) posX = x;
                if (posY == -1 || posY < 0 || posY > screenHeight) posY = y;

                // Créer la bulle Kikimeter
                string logPath = config.KikimeterLogPath ?? "";
                KikimeterIndividualMode individualMode = new KikimeterIndividualMode();
                kikimeterBubble = new GameOverlay.Windows.KikimeterBubble(logPath, individualMode, config, posX, posY, opacity, size);
                // Le menu contextuel est maintenant géré par Windows Forms dans OnContextMenuOpening
                // Le fond est déjà appliqué dans le constructeur

                // Événements
                kikimeterBubble.OnOpenKikimeter += (sender, e) =>
                {
                    try
                    {
                        ToggleKikimeter();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur dans OnOpenKikimeter: {ex.Message}");
                        CustomMessageBox.Show($"Erreur lors de l'ouverture du kikimeter: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                kikimeterBubble.OnOpenLoot += (sender, e) =>
                {
                    try
                    {
                        ToggleLoot();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur dans OnOpenLoot (depuis KikimeterBubble): {ex.Message}");
                        CustomMessageBox.Show($"Erreur lors de l'ouverture du loot: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                kikimeterBubble.OnOpenWeb += (sender, e) =>
                {
                    try
                    {
                        ToggleWeb();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur dans OnOpenWeb: {ex.Message}");
                        CustomMessageBox.Show($"Erreur lors de l'ouverture du navigateur web: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                kikimeterBubble.OnOpenSettings += (sender, e) =>
                {
                    try
                    {
                        ToggleSettingsWindow();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur dans OnOpenSettings: {ex.Message}");
                        CustomMessageBox.Show($"Erreur lors de l'ouverture des paramètres: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                // OnConfigurePath supprimé (menu contextuel retiré)
                kikimeterBubble.PositionChanged += (sender, newPos) =>
                {
                    try
                    {
                        if (kikimeterBubble != null)
                        {
                            Canvas.SetLeft(kikimeterBubble, newPos.X);
                            Canvas.SetTop(kikimeterBubble, newPos.Y);
                            SaveKikimeterBubbleSettings();
                            // Retourner le focus au jeu après déplacement
                            ScheduleFocusReturn();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur dans PositionChanged: {ex.Message}");
                    }
                };
                kikimeterBubble.SizeChanged += (sender, newSize) =>
                {
                    try
                    {
                        // Ne pas appeler UpdateSize ici car cela créerait une boucle infinie
                        // UpdateSize est déjà appelé depuis le menu contextuel
                        // On met juste à jour la position Canvas et on sauvegarde
                        if (kikimeterBubble != null)
                        {
                            SaveKikimeterBubbleSettings();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur dans SizeChanged: {ex.Message}");
                    }
                };
                kikimeterBubble.OpacityChanged += (sender, newOpacity) =>
                {
                    try
                    {
                        // Ne pas appeler UpdateOpacity ici car cela créerait une boucle infinie
                        // UpdateOpacity est déjà appelé depuis le menu contextuel
                        // On met juste à jour le fond et on sauvegarde
                        if (kikimeterBubble != null)
                        {
                            // Mettre à jour le fond avec la couleur de la config
                            kikimeterBubble.UpdateBackgroundWithOpacity(newOpacity, config.BubbleBackgroundColor ?? "#FF1A1A1A");
                            SaveKikimeterBubbleSettings();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur dans OpacityChanged: {ex.Message}");
                    }
                };
                // ZoomChanged supprimé (menu contextuel retiré)
                // kikimeterBubble.IndividualModeChanged += (sender, enable) =>
                // {
                //     if (kikimeterWindow != null)
                //     {
                //         // Toggle le mode individuel
                //         Dispatcher.Invoke(() =>
                //         {
                //             // kikimeterWindow.ToggleIndividualMode();
                //         });
                //     }
                // };
                // DeleteRequested supprimé (menu contextuel retiré)
                // kikimeterBubble.SectionColorChanged += (sender, color) => UpdateKikimeterSectionColor(color);

                // Position
                Canvas.SetLeft(kikimeterBubble, posX);
                Canvas.SetTop(kikimeterBubble, posY);

                MainCanvas.Children.Add(kikimeterBubble);
                this.UpdateLayout();
                
                Logger.Info("MainWindow", $"Bulle Kikimeter créée à ({posX}, {posY})");
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Erreur CreateKikimeterBubble: " + ex.Message);
            }
        }

        private void SaveKikimeterBubbleSettings()
        {
            if (kikimeterBubble != null)
            {
                config.KikimeterBubbleSize = kikimeterBubble.Width;
                config.KikimeterBubbleOpacity = kikimeterBubble.Opacity;
                config.KikimeterBubbleX = (int)Canvas.GetLeft(kikimeterBubble);
                config.KikimeterBubbleY = (int)Canvas.GetTop(kikimeterBubble);
                SaveConfiguration();
                Logger.Debug("MainWindow", $"Kikimeter bubble settings saved: Size={kikimeterBubble.Width}, Opacity={kikimeterBubble.Opacity}");
            }
        }

        private void RemoveKikimeterBubble()
        {
            if (kikimeterBubble != null)
            {
                MainCanvas.Children.Remove(kikimeterBubble);
                kikimeterBubble = null;
                
                // Réinitialiser la position dans la config
                config.KikimeterBubbleX = -1;
                config.KikimeterBubbleY = -1;
                SaveConfiguration();
            }
        }

        private void ToggleKikimeter()
        {
            try
            {
                // Si la fenêtre existe, vérifier son état et toggle
                if (kikimeterWindow != null)
                {
                    // Toujours afficher la fenêtre de base (mode normal)
                    // Si on est en mode individuel, fermer les fenêtres individuelles d'abord
                    var individualCheckbox = kikimeterWindow.FindName("IndividualModeCheckbox") as System.Windows.Controls.CheckBox;
                    bool isIndividualModeActive = individualCheckbox != null && individualCheckbox.IsChecked == true;
                    
                    if (isIndividualModeActive)
                    {
                        // Fermer toutes les fenêtres individuelles avant de toggle
                        kikimeterWindow.CloseAllIndividualWindows();
                        // Désactiver le mode individuel pour revenir au mode normal sans déclencher l'événement
                        kikimeterWindow.SetIndividualMode(false, suppressEvent: true);
                        // S'assurer que la fenêtre principale est visible
                        if (!kikimeterWindow.IsVisible)
                        {
                            kikimeterWindow.ShowFromController(true);
                        }
                    }
                    
                    // Toggle la fenêtre principale
                    if (kikimeterWindow.IsVisible)
                    {
                        kikimeterWindow.HideFromController(true);
                        Logger.Debug("MainWindow", "Fenêtre Kikimeter principale cachée");
                        return;
                    }
                    else
                    {
                        // Fenêtre existe mais est cachée, la réafficher
                        kikimeterWindow.ShowFromController(true);
                        // Réinitialiser l'état du bouton minimize (via FindName)
                        var minimizeBtn = kikimeterWindow.FindName("MinimizeButton") as System.Windows.Controls.Button;
                        if (minimizeBtn != null)
                        {
                            minimizeBtn.Content = "─";
                        }
                        // Réinitialiser l'état minimisé si la fenêtre était minimisée
                        var mainGrid = kikimeterWindow.FindName("MainGrid") as System.Windows.Controls.Grid;
                        if (mainGrid != null && mainGrid.RowDefinitions.Count > 1)
                        {
                            mainGrid.RowDefinitions[1].Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                        }
                        Logger.Debug("MainWindow", "Fenêtre Kikimeter principale réaffichée");
                        return;
                    }
                }

                // Sinon, créer et ouvrir la fenêtre
                ShowKikimeter();
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Erreur ToggleKikimeter: " + ex.Message);
            }
        }

        private void ShowKikimeter()
        {
            try
            {
                if (kikimeterWindow == null)
                {
                    var existing = System.Windows.Application.Current.Windows
                        .OfType<GameOverlay.Kikimeter.KikimeterWindow>()
                        .FirstOrDefault();
                    if (existing != null)
                    {
                        kikimeterWindow = existing;
                    }
                }

                // Charger le mode individuel
                KikimeterIndividualMode individualMode = new KikimeterIndividualMode();
                try
                {
                    var modePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "kikimeter_individual_mode.json");
                    if (File.Exists(modePath))
                    {
                        var json = File.ReadAllText(modePath);
                        individualMode = Newtonsoft.Json.JsonConvert.DeserializeObject<KikimeterIndividualMode>(json) ?? new KikimeterIndividualMode();
                    }
                }
                catch { }
                
                // Créer ou réafficher la fenêtre Kikimeter
                bool createdNow = false;
                if (kikimeterWindow == null)
                {
                    kikimeterWindow = new GameOverlay.Kikimeter.KikimeterWindow(config.KikimeterLogPath ?? "", individualMode);
                    createdNow = true;
                    
                    // S'assurer que la fenêtre a une position et une taille par défaut si rien n'est sauvegardé
                    var savedPosition = LoadWindowPosition("KikimeterWindow");
                    if (savedPosition != null)
                    {
                        kikimeterWindow.Left = savedPosition.Left;
                        kikimeterWindow.Top = savedPosition.Top;
                        kikimeterWindow.Width = savedPosition.Width > 0 ? savedPosition.Width : 400;
                        kikimeterWindow.Height = savedPosition.Height > 0 ? savedPosition.Height : 600;
                    }
                    else
                    {
                        // Position par défaut : centré horizontalement, plus bas verticalement pour voir tout le kikimeter
                        kikimeterWindow.Left = (SystemParameters.PrimaryScreenWidth - 400) / 2;
                        kikimeterWindow.Top = (SystemParameters.PrimaryScreenHeight - 600) / 2 + 150; // Plus bas de 150px
                    }
                    
                    // Sauvegarder la position lors des changements de taille
                    kikimeterWindow.SizeChanged += (s, e) => SaveKikimeterWindowPosition();
                    
                    // Timer pour sauvegarder périodiquement la position (toutes les secondes)
                    var positionTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    positionTimer.Tick += (s, e) => SaveKikimeterWindowPosition();
                    positionTimer.Start();
                    
                    // Sauvegarder quand la fenêtre est déplacée (via mouse up après drag)
                    kikimeterWindow.MouseLeftButtonUp += (s, e) =>
                    {
                        // Petit délai pour s'assurer que la position est mise à jour
                        System.Windows.Threading.DispatcherTimer delayTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(100)
                        };
                        delayTimer.Tick += (s2, e2) =>
                        {
                            SaveKikimeterWindowPosition();
                            delayTimer.Stop();
                        };
                        delayTimer.Start();
                        ScheduleFocusReturn();
                    };
                    
                    kikimeterWindow.Closing += (s, e) =>
                    {
                        e.Cancel = true; // Annuler la fermeture
                        SaveKikimeterWindowPosition();
                        positionTimer.Stop();
                        kikimeterWindow.Hide(); // Masquer à la place
                        // Ne pas mettre kikimeterWindow à null pour pouvoir le rouvrir
                    };
                }

                if (kikimeterWindow == null)
                {
                    return;
                }

                // Appliquer la couleur des sections depuis la config
                if (!string.IsNullOrEmpty(config.KikimeterSectionBackgroundColor))
                {
                    // kikimeterWindow.ApplySectionBackgroundColor(config.KikimeterSectionBackgroundColor);
                }

                // Toujours afficher la fenêtre, même si elle doit être cachée après
                // Cela garantit qu'elle est initialisée correctement
                if (!kikimeterWindow.IsVisible)
                {
                    // Vérifier si le mode individuel est activé
                    bool isIndividualMode = individualMode.IndividualMode;
                    
                    // Si le mode individuel est activé, on montre d'abord la fenêtre puis on la cache
                    // pour que les fenêtres individuelles puissent être créées
                    if (!isIndividualMode)
                    {
                        if (!kikimeterWindow.UserRequestedHidden || createdNow)
                        {
                            kikimeterWindow.ShowFromController(false, resetUserFlag: false);
                        }
                        kikimeterWindow.Activate();
                        kikimeterWindow.Focus();
                    }
                    else
                    {
                        // En mode individuel, on montre brièvement la fenêtre pour l'initialiser
                        // puis elle sera cachée par InitializeWindow() qui appelle ShowIndividualWindows()
                        if (!kikimeterWindow.UserRequestedHidden || createdNow)
                        {
                            kikimeterWindow.ShowFromController(false, resetUserFlag: false);
                            kikimeterWindow.Activate();
                        }
                        // La fenêtre sera cachée automatiquement par InitializeWindow() si le mode est activé
                    }
                }
                else
                {
                    kikimeterWindow.Activate();
                    kikimeterWindow.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur ShowKikimeter: {ex.Message}");
            }
        }

        private void SaveKikimeterWindowPosition()
        {
            if (kikimeterWindow != null && kikimeterWindow.IsVisible)
            {
                SaveWindowPosition("KikimeterWindow", 
                    kikimeterWindow.Left, 
                    kikimeterWindow.Top, 
                    kikimeterWindow.Width, 
                    kikimeterWindow.Height);
            }
        }

        // LootBubble n'est plus utilisée - elle est maintenant intégrée dans KikimeterBubble
        private void CreateLootBubble(int x, int y)
        {
            // Méthode non utilisée - LootBubble est intégrée dans KikimeterBubble
        }

        private void SaveLootBubbleSettings()
        {
            // LootBubble n'est plus utilisée
        }

        private void RemoveLootBubble()
        {
            // LootBubble n'est plus utilisée
        }

        /// <summary>
        /// Met à jour la couleur des sections (rectangles cyan) pour KikimeterWindow
        /// </summary>
        private void UpdateKikimeterSectionColor(string colorHex)
        {
            try
            {
                config.KikimeterSectionBackgroundColor = colorHex;
                
                // Appliquer la couleur à la fenêtre si elle existe
                if (kikimeterWindow != null)
                {
                    // kikimeterWindow.ApplySectionBackgroundColor(colorHex);
                }
                
                // Appliquer aussi aux fenêtres individuelles
                // if (kikimeterWindow != null)
                // {
                //     foreach (var playerWindow in kikimeterWindow.GetPlayerWindows())
                //     {
                //         playerWindow?.ApplySectionBackgroundColor(colorHex);
                //     }
                // }
                
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", "Erreur UpdateKikimeterSectionColor: " + ex.Message);
            }
        }


        private void ConfigureKikimeterLogPath()
        {
            try
            {
                var dialog = new LogPathConfigDialog(
                    "Configurer le chemin du fichier wakfu.log",
                    "Fichier de log Wakfu (*.log)|*.log|Tous les fichiers (*.*)|*.*",
                    config.KikimeterLogPath ?? "");
                
                dialog.Owner = this;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                
                if (dialog.ShowDialog() == true)
                {
                    config.KikimeterLogPath = dialog.LogPath;
                    SaveConfiguration();
                    
                    // Si la fenêtre Kikimeter est ouverte, la redémarrer avec le nouveau chemin
                    if (kikimeterWindow != null)
                    {
                        kikimeterWindow.Hide();
                        kikimeterWindow = null;
                        ShowKikimeter();
                    }
                    
                    CustomMessageBox.Show(
                        "Le chemin a été configuré avec succès. La fenêtre Kikimeter sera redémarrée pour utiliser le nouveau chemin.",
                        "Configuration sauvegardée",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Erreur lors de la configuration: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void ConfigureLootLogPath()
        {
            try
            {
                var dialog = new LogPathConfigDialog(
                    "Configurer le chemin du fichier wakfu_chat.log",
                    "Fichier de log chat Wakfu (*.log)|*.log|Tous les fichiers (*.*)|*.*",
                    config.LootChatLogPath ?? "");
                
                dialog.Owner = this;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                
                if (dialog.ShowDialog() == true)
                {
                    config.LootChatLogPath = dialog.LogPath;
                    SaveConfiguration();
                    
                    // Démarrer le watcher si la LootWindow existe déjà
                    if (lootWindow != null && !string.IsNullOrEmpty(config.LootChatLogPath))
                    {
                        try
                        {
                            string chatLogPath = config.LootChatLogPath ?? "";
                            string kikimeterLogPath = config.KikimeterLogPath ?? "";
                            lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                            Logger.Info("MainWindow", $"Watcher démarré après configuration du chemin: {chatLogPath}");
                            
                            // Initialiser le suivi des ventes
                            InitializeSaleTracker();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("MainWindow", $"Erreur lors du démarrage du watcher après configuration: {ex.Message}");
                        }
                    }
                    // Si la fenêtre n'existe pas encore, elle sera créée au prochain appel à InitializeWindowsInBackground() ou ToggleLoot()
                    
                    CustomMessageBox.Show(
                        "Le chemin a été configuré avec succès.",
                        "Configuration sauvegardée",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Erreur lors de la configuration: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ToggleLoot()
        {
            try
            {
                // Si la fenêtre n'existe pas encore, l'initialiser en arrière-plan d'abord
                if (lootWindow == null)
                {
                    InitializeWindowsInBackground();
                }
                
                if (lootWindow == null || !lootWindow.IsVisible)
                {
                    // Créer ou réouvrir la fenêtre
                    if (lootWindow == null)
                    {
                        lootWindow = new GameOverlay.Kikimeter.Views.LootWindow();
                        lootWindow.ServerSwitched += LootWindow_ServerSwitched;
                        
                        // Intercepter la fermeture pour utiliser Hide() au lieu de Close()
                        lootWindow.Closing += (s, e) =>
                        {
                            e.Cancel = true; // Annuler la fermeture
                            SaveLootWindowPosition();
                            lootWindow.Hide(); // Masquer à la place
                            // Ne pas mettre lootWindow à null pour pouvoir le rouvrir
                        };
                        
                        lootWindow.Closed += (s, e) => 
                        {
                            // Ne rien faire ici car on utilise Closing avec Cancel
                        };
                        
                        // Sauvegarder la position quand la fenêtre est déplacée ou redimensionnée
                        lootWindow.LocationChanged += (s, e) => SaveLootWindowPosition();
                        lootWindow.SizeChanged += (s, e) => SaveLootWindowPosition();
                        
                        // Charger la position sauvegardée
                        LoadLootWindowPosition();
                        lootWindow.Loaded += LootWindow_LoadedForResetHook;
                        
                        // Démarrer le tracking IMMÉDIATEMENT, même si la fenêtre n'est pas visible
                        // Ne pas vérifier File.Exists - LootTracker surveille même si le fichier n'existe pas encore
                        string chatLogPath = config.LootChatLogPath ?? "";
                        string kikimeterLogPath = config.KikimeterLogPath ?? "";
                        if (!string.IsNullOrEmpty(chatLogPath))
                        {
                            lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                            Logger.Info("MainWindow", $"LootWindow.StartWatching appelé dès la création de la fenêtre sur {chatLogPath}");
                            
                            // Initialiser le suivi des ventes
                            InitializeSaleTracker();
                        }
                        else
                        {
                            Logger.Info("MainWindow", "Chemin du log chat non configuré - StartWatching non démarré");
                        }
                    }
                    else
                    {
                        // Si la fenêtre existe déjà mais n'est pas visible, s'assurer que StartWatching est actif
                        if (!lootWindow.IsVisible)
                        {
                            string chatLogPath = config.LootChatLogPath ?? "";
                            string kikimeterLogPath = config.KikimeterLogPath ?? "";
                            if (!string.IsNullOrEmpty(chatLogPath))
                            {
                                // Vérifier si StartWatching n'a pas encore été appelé
                                try
                                {
                                    lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                                    Logger.Info("MainWindow", $"LootWindow.StartWatching appelé pour une fenêtre existante non visible sur {chatLogPath}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Info("MainWindow", $"StartWatching déjà actif ou erreur: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    EnsureLootWindowIsOnScreen();
                    lootWindow.WindowState = WindowState.Normal;
                    lootWindow.Show();
                    lootWindow.Visibility = Visibility.Visible;
                    // Petit "topmost pulse" pour ramener la fenêtre au premier plan sans la bloquer durablement.
                    lootWindow.Topmost = true;
                    lootWindow.Topmost = false;
                    lootWindow.Activate();
                }
                else
                {
                    // Masquer la fenêtre
                    lootWindow.Hide();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur ToggleLoot: {ex.Message}\n{ex}");
                
                // Garder le comportement historique: ne pas réinitialiser agressivement la fenêtre
                // pour éviter de perdre le contexte loot/session après un combat.
                // On tente uniquement de redémarrer le watcher si une instance existe déjà.
                if (lootWindow != null)
                {
                    try
                    {
                        string chatLogPath = config.LootChatLogPath ?? "";
                        string kikimeterLogPath = config.KikimeterLogPath ?? "";
                        if (!string.IsNullOrEmpty(chatLogPath))
                        {
                            lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                            Logger.Info("MainWindow", $"ToggleLoot recovery: StartWatching relancé sur {chatLogPath}");
                        }
                    }
                    catch (Exception restartEx)
                    {
                        Logger.Error("MainWindow", $"ToggleLoot recovery: échec du redémarrage watcher: {restartEx.Message}\n{restartEx}");
                    }
                }

            }
        }
        
        private void LootWindow_LoadedForResetHook(object? sender, RoutedEventArgs e)
        {
            if (sender is not GameOverlay.Kikimeter.Views.LootWindow window)
            {
                return;
            }

            window.Loaded -= LootWindow_LoadedForResetHook;
            window.Dispatcher.BeginInvoke(new Action(() => HookLootWindowResetButton(window)));
        }

        private void HookLootWindowResetButton(GameOverlay.Kikimeter.Views.LootWindow window)
        {
            try
            {
                if (window.FindName("ClearLootButton") is System.Windows.Controls.Button resetButton)
                {
                    resetButton.Click -= LootWindow_ResetButton_ExtraHandler;
                    resetButton.Click += LootWindow_ResetButton_ExtraHandler;
                    Logger.Debug("MainWindow", "Hook supplémentaire du bouton Reset (loot).");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur HookLootWindowResetButton: {ex.Message}");
            }
        }

        private void LootWindow_ResetButton_ExtraHandler(object? sender, RoutedEventArgs e)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var lootDir = Path.Combine(appData, "Amaliassistant", "Loot");
                Directory.CreateDirectory(lootDir);

                var configPath = Path.Combine(lootDir, "loot_characters.json");
                var freshConfig = new LootCharacterConfig
                {
                    LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var json = JsonConvert.SerializeObject(freshConfig, Formatting.Indented);
                File.WriteAllText(configPath, json);
                Logger.Info("MainWindow", $"loot_characters.json réinitialisé ({configPath})");

                if (kikimeterWindow != null)
                {
                    try
                    {
                        kikimeterWindow.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                kikimeterWindow.ResetDisplayFromLoot("Reset déclenché depuis la fenêtre de loot");
                                Logger.Info("MainWindow", "Kikimeter réinitialisé via ResetDisplayFromLoot.");
                            }
                            catch (MissingMethodException)
                            {
                                Logger.Info("MainWindow", "ResetDisplayFromLoot indisponible, aucun reset supplémentaire appliqué.");
                            }
                        });
                    }
                    catch (Exception resetEx)
                    {
                        Logger.Error("MainWindow", $"Erreur lors du reset visuel du Kikimeter: {resetEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la réinitialisation de loot_characters.json: {ex.Message}");
            }
        }

        private void LootWindow_ServerSwitched(object? sender, ServerChangeDetectedEventArgs e)
        {
            try
            {
                var label = string.IsNullOrWhiteSpace(e.ServerName) ? "déconnexion" : e.ServerName;
                Logger.Info("MainWindow", $"Changement de serveur détecté ({label}).");

                // Ne déclencher un reset complet que pour un VRAI changement de serveur.
                // Cela évite de vider les personnages après combat si des événements de reconnexion
                // transientes apparaissent sur le même serveur.
                bool isRealServerChange = !e.IsDisconnect
                    && !string.IsNullOrWhiteSpace(e.ServerName)
                    && !string.Equals(_lastDetectedServerName, e.ServerName, StringComparison.OrdinalIgnoreCase);

                if (!e.IsDisconnect && !string.IsNullOrWhiteSpace(e.ServerName))
                {
                    _lastDetectedServerName = e.ServerName;
                }
                
                // Si c'est une connexion (pas une déconnexion), vider le fichier de log du chat
                // pour ne garder que les nouvelles ventes de cette session
                if (!e.IsDisconnect && !string.IsNullOrWhiteSpace(e.ServerName))
                {
                    try
                    {
                        string? chatLogPath = config.LootChatLogPath;
                        if (!string.IsNullOrEmpty(chatLogPath) && File.Exists(chatLogPath))
                        {
                            // Tronquer le fichier en le vidant
                            File.WriteAllText(chatLogPath, string.Empty);
                            Logger.Info("MainWindow", $"Fichier de log du chat tronqué pour nouvelle session: {chatLogPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        GameOverlay.Models.Logger.Warning("MainWindow", $"Erreur lors du tronquage du fichier de log du chat: {ex.Message}");
                    }
                    
                    // Attendre 2 secondes avant de lire le log pour laisser le jeu écrire l'information
                }
                
                // Si c'est une connexion (pas une déconnexion), afficher la notification de vente
                // On attend un délai pour laisser le jeu écrire l'information dans le log
                if (!e.IsDisconnect && !string.IsNullOrWhiteSpace(e.ServerName))
                {
                    Logger.Info("MainWindow", $"Connexion au serveur détectée: {e.ServerName}");
                    
                    // Réinitialiser le SaleTracker AVANT de lire les notifications de vente
                    InitializeSaleTracker();
                    
                    // Attendre 2 secondes avant de lire le log pour laisser le jeu écrire l'information
                    System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
                    {
                        // S'assurer que l'appel se fait sur le thread UI
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Logger.Info("MainWindow", "Lecture des notifications de vente depuis les lignes récentes");
                            ShowSaleNotificationFromRecentLines();
                        }), DispatcherPriority.Normal);
                    });
                }
                else
                {
                    // Réinitialiser le SaleTracker même en cas de déconnexion
                    InitializeSaleTracker();
                }

                if (isRealServerChange)
                {
                    Logger.Info("MainWindow", $"Vrai changement de serveur détecté ({_lastDetectedServerName}) - reset loot uniquement (personnages/ordre conservés).");
                }
                else
                {
                    Logger.Info("MainWindow", "Événement serveur sur le même serveur: aucun reset personnages/ordre.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors du traitement du changement de serveur: {ex.Message}");
            }
        }
        
        private void LoadLootWindowPosition()
        {
            try
            {
                var positions = PersistentStorageHelper.LoadJsonWithFallback<GameOverlay.Models.WindowPositions>("window_positions.json");
                
                if (positions?.LootWindow != null && lootWindow != null)
                {
                    lootWindow.Left = positions.LootWindow.Left;
                    lootWindow.Top = positions.LootWindow.Top;
                    lootWindow.Width = positions.LootWindow.Width > 0 ? positions.LootWindow.Width : 320;
                    lootWindow.Height = positions.LootWindow.Height > 0 ? positions.LootWindow.Height : 550;
                    EnsureLootWindowIsOnScreen();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur LoadLootWindowPosition: {ex.Message}");
            }
        }

        private void EnsureLootWindowIsOnScreen()
        {
            if (lootWindow == null)
            {
                return;
            }

            double minWidth = lootWindow.MinWidth > 0 ? lootWindow.MinWidth : 320;
            double minHeight = lootWindow.MinHeight > 0 ? lootWindow.MinHeight : 400;
            if (lootWindow.Width < minWidth) lootWindow.Width = minWidth;
            if (lootWindow.Height < minHeight) lootWindow.Height = minHeight;

            var screenLeft = SystemParameters.VirtualScreenLeft;
            var screenTop = SystemParameters.VirtualScreenTop;
            var screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
            var screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

            var margin = 40d;
            var maxLeft = screenRight - lootWindow.Width;
            var maxTop = screenBottom - lootWindow.Height;

            if (double.IsNaN(lootWindow.Left) || double.IsInfinity(lootWindow.Left) ||
                lootWindow.Left < screenLeft - margin || lootWindow.Left > maxLeft + margin)
            {
                lootWindow.Left = Math.Max(screenLeft, (SystemParameters.PrimaryScreenWidth - lootWindow.Width) / 2);
            }

            if (double.IsNaN(lootWindow.Top) || double.IsInfinity(lootWindow.Top) ||
                lootWindow.Top < screenTop - margin || lootWindow.Top > maxTop + margin)
            {
                lootWindow.Top = Math.Max(screenTop, (SystemParameters.PrimaryScreenHeight - lootWindow.Height) / 2);
            }
        }
        
        private void SaveLootWindowPosition()
        {
            try
            {
                if (lootWindow == null) return;
                
                var positions = PersistentStorageHelper.LoadJsonWithFallback<GameOverlay.Models.WindowPositions>("window_positions.json");
                
                positions.LootWindow = new GameOverlay.Models.WindowPosition
                {
                    Left = lootWindow.Left,
                    Top = lootWindow.Top,
                    Width = lootWindow.Width,
                    Height = lootWindow.Height
                };
                
                PersistentStorageHelper.SaveJson("window_positions.json", positions);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur SaveLootWindowPosition: {ex.Message}");
            }
        }
        
        private void TogglePluginManagerWindow()
        {
            try
            {
                if (_pluginManager == null)
                {
                    InitializePluginManager();
                }
                
                if (_pluginManagerWindow == null || !_pluginManagerWindow.IsVisible)
                {
                    if (_pluginManagerWindow == null)
                    {
                        _pluginManagerWindow = new PluginManagerWindow(_pluginManager!);
                    }
                    
                    _pluginManagerWindow.Show();
                    _pluginManagerWindow.Activate();
                    
                    // Gérer la fermeture de la fenêtre
                    _pluginManagerWindow.Closed += (s, e) => { _pluginManagerWindow = null; };
                }
                else
                {
                    _pluginManagerWindow.Hide();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur TogglePluginManagerWindow: {ex.Message}");
            }
        }

        private void ToggleInteractiveMap()
        {
            try
            {
                if (_interactiveMapWindow == null || !_interactiveMapWindow.IsVisible)
                {
                    if (_interactiveMapWindow == null)
                    {
                        _interactiveMapWindow = new InteractiveMapWindow(config, SaveConfiguration);
                    }
                    
                    _interactiveMapWindow.Show();
                    _interactiveMapWindow.Activate();
                    
                    // Gérer la fermeture de la fenêtre
                    _interactiveMapWindow.Closed += (s, e) => { _interactiveMapWindow = null; };
                    
                    Logger.Info("MainWindow", "Carte interactive Wakfu ouverte");
                }
                else
                {
                    _interactiveMapWindow.Hide();
                    Logger.Info("MainWindow", "Carte interactive Wakfu masquée");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur ToggleInteractiveMap: {ex.Message}");
                CustomMessageBox.Show(
                    $"Erreur lors de l'ouverture de la carte interactive:\n\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ToggleSettingsWindow()
        {
            try
            {
                if (settingsWindow == null || !settingsWindow.IsVisible)
                {
                    // Créer ou réouvrir la fenêtre Settings
                    if (settingsWindow == null)
                    {
                        // Récupérer les joueurs actuels depuis PlayerManagementService (SOURCE UNIQUE DE VÉRITÉ)
                        IEnumerable<string>? currentPlayers = null;
                        Func<IEnumerable<string>>? getCurrentPlayers = null;
                        GameOverlay.Kikimeter.Services.PlayerManagementService? playerManagementService = null;
                        
                        if (kikimeterWindow != null && kikimeterWindow.PlayerManagementService != null)
                        {
                            try
                            {
                                playerManagementService = kikimeterWindow.PlayerManagementService;
                                currentPlayers = playerManagementService.GetCurrentPlayerNames().ToList();
                                getCurrentPlayers = () => playerManagementService.GetCurrentPlayerNames();
                                Logger.Info("MainWindow", $"Récupération de {currentPlayers.Count()} joueurs depuis PlayerManagementService pour SettingsWindow");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("MainWindow", $"Impossible de récupérer les joueurs depuis PlayerManagementService: {ex.Message}");
                            }
                        }
                        
                        var accentBrush = ThemeManager.AccentBrush;
                        var sectionBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00));
                        
                        settingsWindow = new GameOverlay.Kikimeter.Views.SettingsWindow(
                            config,
                            (updatedConfig) =>
                            {
                                // Sauvegarder la configuration
                                SaveConfiguration();
                                
                                // Si l'ordre des joueurs a été modifié, l'appliquer à KikimeterWindow
                                if (kikimeterWindow != null && kikimeterWindow.IsVisible)
                                {
                                    try
                                    {
                                        var orderedNames = settingsWindow.GetOrderedNames();
                                        if (orderedNames.Count > 0)
                                        {
                                            // Appliquer l'ordre aux joueurs dans KikimeterWindow
                                            kikimeterWindow.SetPlayerOrder(orderedNames);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error("MainWindow", $"Impossible d'appliquer l'ordre des joueurs: {ex.Message}");
                                    }
                                }
                                
                                // Si les chemins de logs ont changé, redémarrer les fenêtres concernées
                                if (!string.IsNullOrEmpty(updatedConfig.KikimeterLogPath) && 
                                    kikimeterWindow != null && 
                                    kikimeterWindow.IsVisible)
                                {
                                    try
                                    {
                                        kikimeterWindow.Hide();
                                        kikimeterWindow = null;
                                        ShowKikimeter();
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error("MainWindow", $"Impossible de redémarrer KikimeterWindow: {ex.Message}");
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(updatedConfig.LootChatLogPath) && 
                                    lootWindow != null)
                                {
                                    try
                                    {
                                        string chatLogPath = updatedConfig.LootChatLogPath ?? "";
                                        string kikimeterLogPath = updatedConfig.KikimeterLogPath ?? "";
                                        lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error("MainWindow", $"Impossible de redémarrer LootWindow: {ex.Message}");
                                    }
                                }
                            },
                            currentPlayers,
                            getCurrentPlayers,
                            accentBrush,
                            sectionBrush,
                            () => LootWindow_ResetButton_ExtraHandler(null, new RoutedEventArgs()),
                            playerManagementService
                        );
                        
                        // Position par défaut
                        settingsWindow.Left = SystemParameters.PrimaryScreenWidth / 2 - 300;
                        settingsWindow.Top = SystemParameters.PrimaryScreenHeight / 2 - 325;
                    }
                    else
                    {
                        // Fenêtre existe déjà, mettre à jour la liste des joueurs
                        if (kikimeterWindow != null)
                        {
                            try
                            {
                                settingsWindow.UpdatePlayersList();
                                Logger.Info("MainWindow", "Liste des joueurs mise à jour dans SettingsWindow existante");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("MainWindow", $"Erreur lors de la mise à jour de la liste des joueurs: {ex.Message}");
                            }
                        }
                    }
                    
                    settingsWindow.Show();
                    settingsWindow.Activate();
                }
                else
                {
                    // Masquer la fenêtre
                    settingsWindow.Hide();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur ToggleSettingsWindow: {ex.Message}");
            }
        }
        
        private void ToggleWeb()
        {
            try
            {
                if (webWindow == null || !webWindow.IsVisible)
                {
                    // Créer ou réouvrir la fenêtre Web
                    if (webWindow == null)
                    {
                        webWindow = new GameOverlay.Windows.WebWindow(config);
                        
                        // Sauvegarder la config quand la fenêtre Web modifie ses paramètres
                        webWindow.NotifyConfigChanged += () =>
                        {
                            SaveConfiguration();
                        };
                        
                        // Gérer la fermeture de la fenêtre
                        webWindow.Closed += (sender, e) =>
                        {
                            SaveConfiguration();
                            webWindow = null;
                        };
                    }
                    
                    webWindow.Show();
                    webWindow.Activate();
                }
                else
                {
                    // Masquer la fenêtre
                    webWindow.Hide();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur ToggleWeb: {ex.Message}");
                CustomMessageBox.Show($"Erreur lors de l'ouverture du navigateur web: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public void NotifyContextMenuOpened()
        {
            _openContextMenus++;
        }

        public void NotifyContextMenuClosed()
        {
            if (_openContextMenus > 0)
            {
                _openContextMenus--;
            }

            if (_openContextMenus == 0)
            {
                if (_focusReturnPending)
                {
                    _focusReturnPending = false;
                }
                ScheduleFocusReturn();
            }
        }

        public void ScheduleFocusReturn()
        {
            if (_openContextMenus > 0)
            {
                _focusReturnPending = true;
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ReturnFocusToGame();
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Initialise les fenêtres Kikimeter et Loot en arrière-plan pour démarrer la surveillance
        /// même si elles ne sont pas visibles
        /// </summary>
        private void InitializeWindowsInBackground()
        {
            try
            {
                Logger.Info("MainWindow", "Initialisation des fenêtres en arrière-plan pour démarrer la surveillance");
                
                // Initialiser KikimeterWindow si elle n'existe pas encore
                // Créer la fenêtre même si le chemin est vide - on peut le mettre à jour plus tard
                // Si le chemin est vide, ValidateAndFixLogPaths() le trouvera et UpdateLogPath() sera appelé
                if (kikimeterWindow == null)
                {
                    try
                    {
                        KikimeterIndividualMode individualMode = new KikimeterIndividualMode();
                        var modePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "kikimeter_individual_mode.json");
                        if (File.Exists(modePath))
                        {
                            var json = File.ReadAllText(modePath);
                            individualMode = Newtonsoft.Json.JsonConvert.DeserializeObject<KikimeterIndividualMode>(json) ?? new KikimeterIndividualMode();
                        }
                        
                        // Utiliser le chemin si disponible, sinon chaîne vide (sera mis à jour par ValidateAndFixLogPaths si nécessaire)
                        string logPath = config.KikimeterLogPath ?? "";
                        kikimeterWindow = new GameOverlay.Kikimeter.KikimeterWindow(logPath, individualMode);
                        kikimeterWindow.Visibility = Visibility.Hidden; // Créer mais cacher - la fenêtre ne doit pas être visible par défaut
                        kikimeterWindow.ShowInTaskbar = false;
                        
                        // Si le chemin était vide, essayer de le trouver maintenant
                        if (string.IsNullOrEmpty(logPath))
                        {
                            Logger.Info("MainWindow", "Le chemin du log Kikimeter était vide, recherche automatique...");
                            string? foundLogPath = GameOverlay.Kikimeter.Services.WakfuLogFinder.FindFirstLogFile();
                            if (!string.IsNullOrEmpty(foundLogPath))
                            {
                                Logger.Info("MainWindow", $"Chemin du log trouvé automatiquement: {foundLogPath}");
                                config.KikimeterLogPath = foundLogPath;
                                SaveConfiguration();
                                kikimeterWindow.UpdateLogPath(foundLogPath);
                            }
                        }
                        
                        // Configurer la fermeture pour utiliser Hide()
                        kikimeterWindow.Closing += (s, e) =>
                        {
                            e.Cancel = true;
                            kikimeterWindow.Hide();
                        };
                        
                        var savedPosition = LoadWindowPosition("KikimeterWindow");
                        if (savedPosition != null)
                        {
                            kikimeterWindow.Left = savedPosition.Left;
                            kikimeterWindow.Top = savedPosition.Top;
                            kikimeterWindow.Width = savedPosition.Width > 0 ? savedPosition.Width : 400;
                            kikimeterWindow.Height = savedPosition.Height > 0 ? savedPosition.Height : 600;
                        }
                        
                        Logger.Info("MainWindow", "KikimeterWindow créée en arrière-plan - StartWatching déjà démarré dans le constructeur");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors de l'initialisation de KikimeterWindow en arrière-plan: {ex.Message}");
                    }
                }
                
                // Initialiser LootWindow si elle n'existe pas encore
                // TOUJOURS créer la fenêtre au démarrage pour démarrer le watcher immédiatement
                // Même si le chemin n'est pas encore configuré, on peut le démarrer plus tard
                if (lootWindow == null)
                {
                    try
                    {
                        lootWindow = new GameOverlay.Kikimeter.Views.LootWindow();
                        lootWindow.Visibility = Visibility.Hidden; // Créer mais cacher - la fenêtre ne doit pas être visible par défaut
                        lootWindow.ShowInTaskbar = false;
                        lootWindow.ServerSwitched += LootWindow_ServerSwitched;
                        
                        // Configurer la fermeture pour utiliser Hide()
                        lootWindow.Closing += (s, e) =>
                        {
                            e.Cancel = true;
                            SaveLootWindowPosition();
                            lootWindow.Hide();
                        };
                        
                        lootWindow.LocationChanged += (s, e) => SaveLootWindowPosition();
                        lootWindow.SizeChanged += (s, e) => SaveLootWindowPosition();
                        
                        LoadLootWindowPosition();
                        
                        // Démarrer la surveillance immédiatement si le chemin est configuré
                        // LootTracker surveille même si le fichier n'existe pas encore
                        string chatLogPath = config.LootChatLogPath ?? "";
                        string kikimeterLogPath = config.KikimeterLogPath ?? "";
                        if (!string.IsNullOrEmpty(chatLogPath))
                        {
                            lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                            Logger.Info("MainWindow", $"LootWindow créée en arrière-plan - StartWatching démarré sur {chatLogPath}");
                        }
                        else
                        {
                            Logger.Info("MainWindow", "LootWindow créée en arrière-plan mais chemin du log non configuré - StartWatching sera démarré quand le chemin sera configuré");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors de l'initialisation de LootWindow en arrière-plan: {ex.Message}");
                    }
                }
                
                // Initialiser le SaleTracker même si la LootWindow n'est pas créée (si le chemin du log est configuré)
                // Ne pas vérifier File.Exists - le SaleTracker surveille même si le fichier n'existe pas encore
                if (_saleTracker == null && !string.IsNullOrEmpty(config.LootChatLogPath))
                {
                    try
                    {
                        InitializeSaleTracker();
                        Logger.Info("MainWindow", "SaleTracker initialisé en arrière-plan (sans LootWindow)");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors de l'initialisation du SaleTracker en arrière-plan: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur dans InitializeWindowsInBackground: {ex.Message}");
            }
        }

        /// <summary>
        /// Redonne le focus au processus du jeu (Wakfu) après une interaction
        /// </summary>
        private void ReturnFocusToGame()
        {
            // Utiliser un timer pour s'assurer que l'interaction est terminée
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                
                try
                {
                    // Chercher le processus Wakfu
                    var wakfuProcesses = System.Diagnostics.Process.GetProcessesByName("Wakfu")
                        .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero)
                        .OrderByDescending(p => p.MainWindowTitle.Contains("Wakfu"))
                        .ToList();
                    
                    if (wakfuProcesses.Any())
                    {
                        var wakfuProcess = wakfuProcesses.First();
                        IntPtr hwnd = wakfuProcess.MainWindowHandle;
                        
                        if (hwnd != IntPtr.Zero)
                        {
                            ShowWindow(hwnd, 9); // SW_RESTORE
                            SetForegroundWindow(hwnd);
                            Logger.Debug("MainWindow", $"Focus retourné au jeu: {wakfuProcess.MainWindowTitle}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug("MainWindow", $"Erreur lors du retour du focus: {ex.Message}");
                }
            };
            
            timer.Start();
        }
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            ScheduleFocusReturn();
        }

        protected override void OnPreviewKeyUp(WpfKeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            ScheduleFocusReturn();
        }

        // Méthodes supprimées : CreateBubble, CreateAddSiteBubble, CreateDefaultBubbles, etc. - fonctionnalité sites web retirée
        // Méthode supprimée : CreateBubble - fonctionnalité sites web retirée

        /// <summary>
        /// Valide et corrige automatiquement les chemins de log si les fichiers n'existent pas
        /// </summary>
        private bool ValidateAndFixLogPaths()
        {
            bool configChanged = false;

            try
            {
                // Vérifier et corriger le chemin du log Kikimeter (wakfu.log)
                if (string.IsNullOrEmpty(config.KikimeterLogPath) || !File.Exists(config.KikimeterLogPath))
                {
                    Logger.Info("MainWindow", "Le chemin du log Kikimeter est invalide ou vide, recherche automatique...");
                    string? foundLogPath = GameOverlay.Kikimeter.Services.WakfuLogFinder.FindFirstLogFile();
                    
                    if (!string.IsNullOrEmpty(foundLogPath) && File.Exists(foundLogPath))
                    {
                        Logger.Info("MainWindow", $"Chemin du log Kikimeter trouvé automatiquement: {foundLogPath}");
                        config.KikimeterLogPath = foundLogPath;
                        configChanged = true;
                    }
                    else
                    {
                        GameOverlay.Models.Logger.Warning("MainWindow", "Aucun fichier wakfu.log trouvé automatiquement. Veuillez le configurer manuellement dans les paramètres.");
                    }
                }
                else
                {
                    Logger.Debug("MainWindow", $"Chemin du log Kikimeter valide: {config.KikimeterLogPath}");
                }

                // Vérifier et corriger le chemin du log Loot (wakfu_chat.log)
                if (string.IsNullOrEmpty(config.LootChatLogPath) || !File.Exists(config.LootChatLogPath))
                {
                    Logger.Info("MainWindow", "Le chemin du log Loot est invalide ou vide, recherche automatique...");
                    
                    // Essayer de trouver wakfu_chat.log à partir du chemin wakfu.log si disponible
                    string? chatLogPath = null;
                    if (!string.IsNullOrEmpty(config.KikimeterLogPath))
                    {
                        chatLogPath = GameOverlay.Kikimeter.Services.WakfuLogFinder.FindChatLogFile(config.KikimeterLogPath);
                    }
                    
                    // Si pas trouvé, chercher tous les fichiers de log et essayer de trouver wakfu_chat.log
                    if (string.IsNullOrEmpty(chatLogPath) || !File.Exists(chatLogPath))
                    {
                        var allLogFiles = GameOverlay.Kikimeter.Services.WakfuLogFinder.FindAllLogFiles();
                        foreach (var logFile in allLogFiles)
                        {
                            string candidateChatLog = logFile.Replace("wakfu.log", "wakfu_chat.log");
                            if (File.Exists(candidateChatLog))
                            {
                                chatLogPath = candidateChatLog;
                                break;
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(chatLogPath) && File.Exists(chatLogPath))
                    {
                        Logger.Info("MainWindow", $"Chemin du log Loot trouvé automatiquement: {chatLogPath}");
                        config.LootChatLogPath = chatLogPath;
                        configChanged = true;
                    }
                    else
                    {
                        GameOverlay.Models.Logger.Warning("MainWindow", "Aucun fichier wakfu_chat.log trouvé automatiquement. Veuillez le configurer manuellement dans les paramètres.");
                    }
                }
                else
                {
                    Logger.Debug("MainWindow", $"Chemin du log Loot valide: {config.LootChatLogPath}");
                }

                // Sauvegarder la configuration si des changements ont été faits
                if (configChanged)
                {
                    SaveConfiguration();
                    Logger.Info("MainWindow", "Configuration mise à jour avec les nouveaux chemins de log");
                }
                
                return configChanged;
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la validation des chemins de log: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Redémarre les watchers avec les nouveaux chemins après une mise à jour automatique
        /// </summary>
        private void RestartWatchersWithNewPaths()
        {
            try
            {
                // Redémarrer KikimeterWindow si elle existe
                if (kikimeterWindow != null && !string.IsNullOrEmpty(config.KikimeterLogPath))
                {
                    try
                    {
                        Logger.Info("MainWindow", $"Redémarrage du watcher Kikimeter avec le nouveau chemin: {config.KikimeterLogPath}");
                        kikimeterWindow.UpdateLogPath(config.KikimeterLogPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors du redémarrage du watcher Kikimeter: {ex.Message}");
                    }
                }
                
                // Redémarrer LootWindow si elle existe
                if (lootWindow != null && !string.IsNullOrEmpty(config.LootChatLogPath))
                {
                    try
                    {
                        Logger.Info("MainWindow", $"Redémarrage du watcher Loot avec les nouveaux chemins: {config.LootChatLogPath}, {config.KikimeterLogPath}");
                        string chatLogPath = config.LootChatLogPath ?? "";
                        string kikimeterLogPath = config.KikimeterLogPath ?? "";
                        lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors du redémarrage du watcher Loot: {ex.Message}");
                    }
                }
                
                // Redémarrer SaleTracker si nécessaire
                if (!string.IsNullOrEmpty(config.LootChatLogPath))
                {
                    try
                    {
                        // Arrêter l'ancien tracker s'il existe
                        if (_saleTracker != null)
                        {
                            _saleTracker.Dispose();
                            _saleTracker = null;
                        }
                        
                        // Recréer le tracker avec le nouveau chemin
                        InitializeSaleTracker();
                        Logger.Info("MainWindow", $"SaleTracker redémarré avec le nouveau chemin: {config.LootChatLogPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors du redémarrage du SaleTracker: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors du redémarrage des watchers: {ex.Message}");
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                string configDir = Path.GetDirectoryName(configFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // Créer une sauvegarde avant d'écrire la nouvelle configuration
                string backupConfigFile = configFile + ".backup";
                if (File.Exists(configFile))
                {
                    try
                    {
                        File.Copy(configFile, backupConfigFile, overwrite: true);
                    }
                    catch
                    {
                        // Ignorer les erreurs de sauvegarde, ce n'est pas critique
                    }
                }

                // Sauvegarder uniquement la configuration Kikimeter et Loot
                string jsonOutput = JsonConvert.SerializeObject(config, Formatting.Indented);
                
                // Écrire dans un fichier temporaire d'abord, puis renommer pour éviter la corruption
                string tempConfigFile = configFile + ".tmp";
                File.WriteAllText(tempConfigFile, jsonOutput);
                
                // Remplacer le fichier principal seulement si l'écriture a réussi
                File.Move(tempConfigFile, configFile, overwrite: true);
                
                Logger.Debug("MainWindow", "Configuration sauvegardée avec succès");
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la sauvegarde de la configuration: {ex.Message}");
                CustomMessageBox.Show($"Erreur sauvegarde: {ex.Message}");
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Afficher le menu contextuel Windows Forms (comme le NotifyIcon)
            if (mainWindowContextMenu != null)
            {
                // Convertir la position WPF en position écran
                var point = this.PointToScreen(e.GetPosition(this));
                mainWindowContextMenu.Show((int)point.X, (int)point.Y);
            }
        }

        private void Window_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                ToggleOverlay();
            }
            // F9 supprimé - fonctionnalité sites web retirée
        }


        // Méthodes supprimées : ToggleAllMinimizedWindows, AddSite_Click - fonctionnalité sites web retirée


        public void HideOverlay_Click(object sender, EventArgs e)
        {
            ToggleOverlay();
        }
        
        public void TestSaleNotification_Click(object sender, EventArgs e)
        {
            TestSaleNotification();
        }

        public void Exit_Click(object sender, EventArgs e)
        {
            ExitApplication();
        }

        public void OnThemeChanged()
        {
            try
            {
                // Mettre à jour les éléments créés dynamiquement qui utilisent la couleur d'accent
                UpdateDynamicThemeElements();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de la mise à jour du thème: {ex.Message}");
            }
        }

        private void UpdateDynamicThemeElements()
        {
            try
            {
                // Les bulles sites web et musique ont été supprimées - fonctionnalités retirées
                // Cette méthode est conservée pour compatibilité mais ne fait plus rien
                // Invalider le canvas pour forcer le redessinage
                MainCanvas.InvalidateVisual();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur UpdateDynamicThemeElements: {ex.Message}");
            }
        }

        private void OpenColorPicker()
        {
            try
            {
                using (var colorDialog = new FormsColorDialog())
                {
                    // Définir la couleur actuelle
                    var currentColor = ThemeManager.AccentColor;
                    colorDialog.Color = System.Drawing.Color.FromArgb(
                        currentColor.R, 
                        currentColor.G, 
                        currentColor.B);
                    
                    colorDialog.FullOpen = true; // Afficher toutes les options
                    colorDialog.AllowFullOpen = true;
                    
                    if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var selectedColor = colorDialog.Color;
                        ThemeManager.SetAccentColor(
                            selectedColor.R, 
                            selectedColor.G, 
                            selectedColor.B);
                        
                        // Mettre à jour tous les éléments dynamiques
                        UpdateDynamicThemeElements();
                        
                        // Sauvegarder en hexadécimal
                        config.AccentColorHex = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
                        SaveConfiguration();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur lors de la sélection de couleur : {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SyncMainWindowContextMenuTheme(WpfContextMenu contextMenu)
        {
            try
            {
                ThemeManager.ApplyContextMenuTheme(contextMenu);
            }
            catch { }
        }
        
        private void OpenBubbleBackgroundColorPicker()
        {
            try
            {
                using (var colorDialog = new FormsColorDialog())
                {
                    // Définir la couleur actuelle depuis la config
                    string currentColorHex = config.BubbleBackgroundColor ?? "#FF1A1A1A";
                    var currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColorHex);
                    
                    colorDialog.Color = System.Drawing.Color.FromArgb(
                        currentColor.A,
                        currentColor.R, 
                        currentColor.G, 
                        currentColor.B);
                    
                    colorDialog.FullOpen = true; // Afficher toutes les options
                    colorDialog.AllowFullOpen = true;
                    
                    if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var selectedColor = colorDialog.Color;
                        
                        // Sauvegarder en hexadécimal avec alpha
                        config.BubbleBackgroundColor = $"#{selectedColor.A:X2}{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
                        SaveConfiguration();
                        
                        // Déclencher l'événement pour synchroniser toutes les bulles
                        ThemeManager.BubbleBackgroundColor = config.BubbleBackgroundColor;
                        // Mettre à jour toutes les bulles existantes (pour compatibilité)
                        UpdateAllBubblesBackgroundColor(config.BubbleBackgroundColor);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur OpenBubbleBackgroundColorPicker: {ex.Message}");
            }
        }
        
        private void UpdateAllBubblesBackgroundColor(string colorHex)
        {
            try
            {
                // Mettre à jour KikimeterBubble
                if (kikimeterBubble != null)
                {
                    kikimeterBubble.UpdateBackgroundWithOpacity(kikimeterBubble.Opacity, colorHex);
                }
                
                // Mettre à jour LootBubble
                if (lootBubble != null)
                {
                    lootBubble.UpdateBackgroundWithOpacity(lootBubble.Opacity, colorHex);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur UpdateAllBubblesBackgroundColor: {ex.Message}");
            }
        }

        // Méthodes supprimées : BubbleOpacity*_Click, ToggleWebWindow, CreateNewWebWindow - fonctionnalité sites web retirée

        private void PreventBackgroundThrottling()
        {
            try
            {
                // Empêcher le throttling des processus en arrière-plan sur Windows 11
                var process = System.Diagnostics.Process.GetCurrentProcess();
                process.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
                
                System.Diagnostics.Debug.WriteLine("Protection contre le throttling activée");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur prévention throttling: {ex.Message}");
            }
        }

        private void OptimizeCompositor()
        {
            try
            {
                // Optimiser le compositor de visualisation Windows 11
                // RenderOptions.ProcessRenderMode = RenderMode.Default;
                // RenderOptions.BitmapScalingMode = BitmapScalingMode.HighQuality;
                // RenderOptions.EdgeMode = EdgeMode.Aliased;
                
                // Forcer la mise à jour du compositor
                this.InvalidateVisual();
                
                System.Diagnostics.Debug.WriteLine("Compositor optimisé pour Windows 11");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur optimisation compositor: {ex.Message}");
            }
        }

        // Méthodes supprimées : CreateWebWindowOptimized, ShowWindowAsync - fonctionnalité sites web retirée

        private void SaveWindowPosition(string url, double left, double top, double width, double height)
        {
            try
            {
                // Ne pas sauvegarder si la fenêtre est hors écran (cachée pour PIP)
                if (left < -5000 || top < -5000)
                {
                    System.Diagnostics.Debug.WriteLine($"Position hors écran ignorée pour {url}: {left}, {top}");
                    return;
                }
                
                windowPositions[url] = new WindowPosition
                {
                    Left = left,
                    Top = top,
                    Width = width,
                    Height = height
                };
                
                SaveWindowPositionsToFile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur SaveWindowPosition: {ex.Message}");
            }
        }

        private WindowPosition LoadWindowPosition(string url)
        {
            try
            {
                if (windowPositions.ContainsKey(url))
                {
                    return windowPositions[url];
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur LoadWindowPosition: {ex.Message}");
                return null;
            }
        }

        private void SaveWindowPositionsToFile()
        {
            try
            {
                var positionsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "window_positions.json");
                string positionsDir = Path.GetDirectoryName(positionsFile);
                if (!Directory.Exists(positionsDir))
                {
                    Directory.CreateDirectory(positionsDir);
                }
                var json = JsonConvert.SerializeObject(windowPositions, Formatting.Indented);
                File.WriteAllText(positionsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur SaveWindowPositionsToFile: {ex.Message}");
            }
        }

        private void LoadWindowPositionsFromFile()
        {
            try
            {
                var positionsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "window_positions.json");
                if (File.Exists(positionsFile))
                {
                    var json = File.ReadAllText(positionsFile);
                    windowPositions = JsonConvert.DeserializeObject<Dictionary<string, WindowPosition>>(json) ?? new Dictionary<string, WindowPosition>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur LoadWindowPositionsFromFile: {ex.Message}");
                windowPositions = new Dictionary<string, WindowPosition>();
            }
        }


        // Méthodes pour les nouvelles bulles
        public void StartBubbleDrag(Border bubble, MouseButtonEventArgs e)
        {
            // Gestion du déplacement des bulles
            bool isDragging = false;
            WpfPoint lastMousePosition;

            bubble.MouseLeftButtonDown += (s, args) =>
            {
                if (args.ClickCount == 1)
                {
                    isDragging = true;
                    lastMousePosition = args.GetPosition(this);
                    bubble.CaptureMouse();
                }
            };

            bubble.MouseMove += (s, args) =>
            {
                if (isDragging)
                {
                    WpfPoint currentPosition = args.GetPosition(this);
                    double deltaX = currentPosition.X - lastMousePosition.X;
                    double deltaY = currentPosition.Y - lastMousePosition.Y;

                    Canvas.SetLeft(bubble, Canvas.GetLeft(bubble) + deltaX);
                    Canvas.SetTop(bubble, Canvas.GetTop(bubble) + deltaY);

                    lastMousePosition = currentPosition;
                    SaveConfiguration();
                }
            };

            bubble.MouseLeftButtonUp += (s, args) =>
            {
                isDragging = false;
                bubble.ReleaseMouseCapture();
            ScheduleFocusReturn();
            };
        }

        /// <summary>
        /// Affiche une notification avec les informations de vente
        /// </summary>
        /// <param name="saleInfo">Informations de vente</param>
        /// <param name="showAbsenceMessage">Si true, ajoute "pendant votre absence" au message</param>
        private void ShowSaleNotification(SaleInfo saleInfo, bool showAbsenceMessage = false)
        {
            try
            {
                // Afficher la notification sur le thread UI
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var notificationWindow = new GameOverlay.Kikimeter.Views.SaleNotificationWindow(
                            saleInfo,
                            showAbsenceMessage,
                            config?.SaleNotificationVolume ?? 100);
                        
                        // Nettoyer les fenêtres fermées de la liste
                        _saleNotificationWindows.RemoveAll(w => w == null || !w.IsLoaded);
                        
                        // Positionner la nouvelle fenêtre à la même position que les autres (superposition)
                        var screenWidth = SystemParameters.PrimaryScreenWidth;
                        const double topPosition = 20; // Position fixe pour toutes les fenêtres
                        
                        // La position sera chargée depuis la sauvegarde dans SaleNotificationWindow
                        // Si aucune position sauvegardée, utiliser la position par défaut
                        notificationWindow.Loaded += (s, e) =>
                        {
                            // Si la position n'a pas été chargée depuis la sauvegarde, utiliser la position par défaut
                            if (notificationWindow.Left == 0 && notificationWindow.Top == 0)
                            {
                                notificationWindow.Left = screenWidth - notificationWindow.ActualWidth - 20;
                                notificationWindow.Top = topPosition;
                            }
                        };
                        
                        // Gérer la fermeture
                        notificationWindow.Closed += (s, e) =>
                        {
                            _saleNotificationWindows.Remove(notificationWindow);
                            // Réorganiser les fenêtres restantes (le timer de la nouvelle notification au-dessus démarrera)
                            ReorganizeSaleNotifications();
                        };
                        
                        // Ajouter en début de liste (première = la plus récente = au-dessus)
                        _saleNotificationWindows.Insert(0, notificationWindow);
                        
                        // S'assurer que la nouvelle fenêtre est au-dessus
                        notificationWindow.Show();
                        
                        // Forcer la fenêtre à être toujours visible même en plein écran
                        notificationWindow.Topmost = false;
                        notificationWindow.Topmost = true;
                        
                        // Utiliser Show() et Activate() pour forcer la visibilité
                        notificationWindow.Show();
                        notificationWindow.Activate();
                        
                        // Forcer à nouveau Topmost après un court délai pour garantir la visibilité
                        System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                notificationWindow.Topmost = false;
                                notificationWindow.Topmost = true;
                                notificationWindow.Show();
                            }), DispatcherPriority.Normal);
                        });
                        
                        // Réorganiser le z-order de toutes les fenêtres (la plus récente au-dessus)
                        ReorganizeSaleNotificationsZOrder();
                        
                        Logger.Info("MainWindow", $"Notification de vente affichée: {saleInfo.ItemCount} items pour {saleInfo.TotalKamas} kamas");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors de l'affichage de la notification de vente: {ex.Message}");
                    }
                }), DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de l'affichage de la notification de vente: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Réorganise la position des notifications après la fermeture d'une fenêtre
        /// </summary>
        private void ReorganizeSaleNotifications()
        {
            // Les fenêtres restent à la même position (superposition)
            // On réorganise juste le z-order
            ReorganizeSaleNotificationsZOrder();
        }
        
        /// <summary>
        /// Réorganise le z-order des fenêtres de notification (la plus récente au-dessus)
        /// </summary>
        private void ReorganizeSaleNotificationsZOrder()
        {
            // Arrêter tous les timers de fermeture
            foreach (var window in _saleNotificationWindows)
            {
                if (window != null && window.IsLoaded)
                {
                    window.StopAutoCloseTimer();
                    window.Topmost = false;
                }
            }
            
            // Puis remettre en Topmost=true dans l'ordre inverse (première = au-dessus)
            // Et démarrer le timer seulement pour la notification visible (la première)
            for (int i = _saleNotificationWindows.Count - 1; i >= 0; i--)
            {
                var window = _saleNotificationWindows[i];
                if (window != null && window.IsLoaded)
                {
                    window.Topmost = true;
                    // Démarrer le timer seulement pour la notification au-dessus (la première dans l'ordre inverse)
                    if (i == _saleNotificationWindows.Count - 1)
                    {
                        window.StartAutoCloseTimer();
                    }
                }
            }
        }
        
        /// <summary>
        /// Affiche une notification avec les informations de vente depuis la première ligne du log de chat (lors de la connexion)
        /// </summary>
        private void ShowSaleNotificationFromFirstLine()
        {
            try
            {
                string? chatLogPath = config.LootChatLogPath;
                if (string.IsNullOrWhiteSpace(chatLogPath) || !File.Exists(chatLogPath))
                {
                    Logger.Debug("MainWindow", "Chemin du log de chat non configuré ou fichier inexistant, notification de vente ignorée");
                    return;
                }
                
                // Lire les informations de vente depuis la première ligne du log
                var saleInfo = SaleNotificationService.ReadSaleInfoFromFirstLine(chatLogPath);
                if (saleInfo == null)
                {
                    Logger.Debug("MainWindow", "Aucune information de vente trouvée dans la première ligne du log");
                    return;
                }
                
                // Pour la connexion, afficher "pendant votre absence"
                ShowSaleNotification(saleInfo, showAbsenceMessage: true);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la récupération des informations de vente: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Affiche une notification de vente basée sur les dernières lignes du log de chat
        /// Utile pour détecter les ventes qui apparaissent lors de la connexion
        /// </summary>
        private void ShowSaleNotificationFromRecentLines()
        {
            try
            {
                string? chatLogPath = config.LootChatLogPath;
                if (string.IsNullOrWhiteSpace(chatLogPath))
                {
                    Logger.Info("MainWindow", "Chemin du log de chat non configuré, notification de vente ignorée");
                    return;
                }
                
                if (!File.Exists(chatLogPath))
                {
                    Logger.Info("MainWindow", $"Fichier de log de chat inexistant: {chatLogPath}, notification de vente ignorée");
                    return;
                }
                
                Logger.Info("MainWindow", $"Lecture des notifications de vente depuis: {chatLogPath}");
                
                // Lire les informations de vente depuis les dernières lignes du log (plus récentes)
                var saleInfo = SaleNotificationService.ReadSaleInfoFromRecentLines(chatLogPath, maxLinesToRead: 50);
                if (saleInfo == null)
                {
                    Logger.Info("MainWindow", "Aucune information de vente trouvée dans les dernières lignes du log");
                    return;
                }
                
                Logger.Info("MainWindow", $"Notification de vente trouvée: {saleInfo.ItemCount} items pour {saleInfo.TotalKamas} kamas");
                
                // Pour la connexion, afficher "pendant votre absence"
                ShowSaleNotification(saleInfo, showAbsenceMessage: true);
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la récupération des informations de vente: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Initialise le PluginManager pour charger et gérer les plugins
        /// </summary>
        private void InitializePluginManager()
        {
            try
            {
                if (_pluginManager == null)
                {
                    _pluginManager = new PluginManager();
                    _pluginManager.Initialize(config);
                    // Passer l'action de sauvegarde pour que les changements de plugins soient sauvegardés
                    _pluginManager.SetSaveConfigAction(SaveConfiguration);
                    Logger.Info("MainWindow", "PluginManager initialisé");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de l'initialisation du PluginManager: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialise le service de suivi des ventes en temps réel
        /// </summary>
        private void InitializeSaleTracker()
        {
            try
            {
                // Désactiver l'ancien tracker s'il existe
                if (_saleTracker != null)
                {
                    _saleTracker.SaleDetected -= SaleTracker_SaleDetected;
                    _saleTracker.Dispose();
                    _saleTracker = null;
                }
                
                // Arrêter l'ancien timer s'il existe
                if (_saleTrackerTimer != null)
                {
                    _saleTrackerTimer.Stop();
                    _saleTrackerTimer.Tick -= SaleTrackerTimer_Tick;
                    _saleTrackerTimer = null;
                }
                
                string? chatLogPath = config.LootChatLogPath;
                if (string.IsNullOrWhiteSpace(chatLogPath))
                {
                    Logger.Debug("MainWindow", "Chemin du log de chat non configuré, SaleTracker non initialisé");
                    return;
                }
                
                // Le SaleTracker surveille maintenant même si le fichier n'existe pas encore
                if (!File.Exists(chatLogPath))
                {
                    Logger.Info("MainWindow", $"Fichier de log chat n'existe pas encore: {chatLogPath} - SaleTracker surveillera la création du fichier");
                }
                
                _saleTracker = new GameOverlay.Kikimeter.Services.SaleTracker(chatLogPath);
                _saleTracker.SaleDetected += SaleTracker_SaleDetected;
                
                // Créer et démarrer le timer pour la lecture périodique
                // Interval augmenté à 100ms pour mieux gérer les verrouillages de fichier
                // Le FileSystemWatcher s'occupe des événements rapides, le timer est un backup
                _saleTrackerTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _saleTrackerTimer.Tick += SaleTrackerTimer_Tick;
                _saleTrackerTimer.Start();
                
                Logger.Info("MainWindow", $"SaleTracker initialisé pour la détection des ventes en temps réel (fichier: {chatLogPath}, interval: 100ms)");
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de l'initialisation du SaleTracker: {ex.Message}");
            }
        }
        
        private void SaleTrackerTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (_saleTracker != null)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            _saleTracker.ManualRead();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("MainWindow", $"Erreur lors de ManualRead du SaleTracker: {ex.Message}");
                        }
                    });
                }
                else
                {
                    GameOverlay.Models.Logger.Warning("MainWindow", "SaleTrackerTimer_Tick: _saleTracker est null");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur dans SaleTrackerTimer_Tick: {ex.Message}");
            }
        }
        
        private void SaleTracker_SaleDetected(object? sender, SaleInfo saleInfo)
        {
            Logger.Info("MainWindow", $"Événement SaleDetected reçu: {saleInfo.ItemCount} items pour {saleInfo.TotalKamas} kamas");
            ShowSaleNotification(saleInfo);
        }
        
        /// <summary>
        /// Teste l'affichage d'une notification de vente avec des données fictives
        /// </summary>
        private void TestSaleNotification()
        {
            try
            {
                var testSaleInfo = new SaleInfo(
                    itemCount: new Random().Next(1, 10),
                    totalKamas: new Random().Next(1000, 100000)
                );
                ShowSaleNotification(testSaleInfo, showAbsenceMessage: false);
                Logger.Info("MainWindow", "Notification de vente de test affichée");
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors du test de notification: {ex.Message}");
            }
        }

        // Méthodes supprimées : Toutes les méthodes liées aux sites web et enfants ont été supprimées - fonctionnalité sites web retirée

        /// <summary>
        /// Vérifie si c'est la première installation et affiche un message de bienvenue si nécessaire
        /// MÉTHODE DÉSACTIVÉE - Message de bienvenue supprimé comme demandé par l'utilisateur
        /// </summary>
        private void CheckAndShowWelcomeMessage()
        {
            // MÉTHODE COMPLÈTEMENT DÉSACTIVÉE - Plus de message de bienvenue
            // Créer simplement le fichier de flag pour éviter tout problème
            try
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant");
                string firstRunFlagFile = Path.Combine(appDataDir, "first_run_completed.flag");
                
                // Créer le dossier AppData s'il n'existe pas
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
                
                // Créer le fichier de flag pour indiquer que la première installation est terminée
                if (!File.Exists(firstRunFlagFile))
                {
                    File.WriteAllText(firstRunFlagFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Logger.Info("MainWindow", "Fichier de flag de première installation créé (sans message de bienvenue)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur lors de la vérification du message de bienvenue: {ex.Message}");
            }
        }

    }
}



