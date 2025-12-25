using GameOverlay.Models;
using GameOverlay.Themes;
using GameOverlay.Windows;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using GameOverlay.Kikimeter.Views;
using GameOverlay.Kikimeter.Services;
using GameOverlay.Kikimeter.Models;
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
        private Config config = new Config();
        
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

        private int _openContextMenus;
        private bool _focusReturnPending;

        public MainWindow()
        {
            try
            {
                Logger.Info("MainWindow", "Initialisation de MainWindow");
                
                InitializeComponent();
                
                // Appliquer le thème au menu contextuel de la fenêtre
                if (this.ContextMenu != null)
                {
                    GameOverlay.Themes.ThemeManager.ApplyContextMenuTheme(this.ContextMenu);
                }

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
                        LoadWindowPositionsFromFile();
                        LoadConfiguration();
                        
                        // Message de bienvenue désactivé (demandé par l'utilisateur)
                        // CheckAndShowWelcomeMessage();
                        
                        // Créer les fenêtres au démarrage pour démarrer la surveillance même si elles ne sont pas visibles
                        InitializeWindowsInBackground();
                        
                        // Initialiser le SaleTracker après le chargement de la configuration
                        if (!string.IsNullOrEmpty(config.LootChatLogPath) && File.Exists(config.LootChatLogPath))
                        {
                            InitializeSaleTracker();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", "Erreur dans l'événement Loaded: " + ex.Message);
                    }
                };
                
                // Exclure l'overlay d'Alt+Tab
                this.SourceInitialized += MainWindow_SourceInitialized;

                // Nettoyer les ressources à la fermeture
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
                
                System.Windows.MessageBox.Show("Bulle Kikimeter recréée au centre.", "Information", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur: {ex.Message}", "Erreur", 
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
                
                System.Windows.MessageBox.Show("Bulle Loot recréée au centre.", "Information", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur: {ex.Message}", "Erreur", 
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

                // Créer le menu contextuel
                var contextMenu = new ContextMenuStrip();
                contextMenu.Renderer = new DarkMenuRenderer();
                contextMenu.BackColor = System.Drawing.Color.FromArgb(246, 231, 169); // #FFF6E7A9 - fond
                contextMenu.ForeColor = System.Drawing.Color.FromArgb(110, 92, 42); // #FF6E5C2A - contour
                
                var kikimeterItem = new ToolStripMenuItem("📊 Ouvrir le Kikimeter");
                kikimeterItem.Click += (s, e) => ToggleKikimeter();
                contextMenu.Items.Add(kikimeterItem);

                var lootItem = new ToolStripMenuItem("💎 Ouvrir le Loot");
                lootItem.Click += (s, e) => ToggleLoot();
                contextMenu.Items.Add(lootItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                var settingsItem = new ToolStripMenuItem("⚙️ Paramètres");
                settingsItem.Click += (s, e) => ToggleSettingsWindow();
                contextMenu.Items.Add(settingsItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Option de lancement automatique
                var startupItem = new ToolStripMenuItem("🚀 Lancer au démarrage");
                startupItem.CheckOnClick = true;
                startupItem.Checked = IsStartupEnabled();
                startupItem.Click += (s, e) => ToggleStartup();
                contextMenu.Items.Add(startupItem);

                // Option de vérification des mises à jour
                var updateItem = new ToolStripMenuItem("🔄 Vérifier les mises à jour");
                updateItem.Click += (s, e) => CheckForUpdatesManually();
                contextMenu.Items.Add(updateItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                var exitItem = new ToolStripMenuItem("❌ Quitter");
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

        // Renderer personnalisé pour le menu sombre
        private class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColorTable()) { }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                // Fond : #FFF6E7A9 (RGB: 246, 231, 169)
                var backgroundColor = System.Drawing.Color.FromArgb(246, 231, 169);
                // Contour : #FF6E5C2A (RGB: 110, 92, 42)
                var borderColor = System.Drawing.Color.FromArgb(110, 92, 42);
                
                if (e.Item.Selected)
                {
                    // Couleur de survol : contour avec 15% d'opacité
                    var hoverColor = System.Drawing.Color.FromArgb((int)(255 * 0.15), borderColor.R, borderColor.G, borderColor.B);
                    e.Graphics.FillRectangle(new System.Drawing.SolidBrush(hoverColor), e.Item.ContentRectangle);
                }
                else
                {
                    // Fond
                    e.Graphics.FillRectangle(new System.Drawing.SolidBrush(backgroundColor), e.Item.ContentRectangle);
                }
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                // Séparateur couleur contour : #FF6E5C2A
                var borderColor = System.Drawing.Color.FromArgb(110, 92, 42);
                e.Graphics.DrawLine(new System.Drawing.Pen(borderColor), 
                    e.Item.ContentRectangle.Left + 5, 
                    e.Item.ContentRectangle.Height / 2, 
                    e.Item.ContentRectangle.Right - 5, 
                    e.Item.ContentRectangle.Height / 2);
            }
        }

        // Table de couleurs personnalisée - couleurs de la fenêtre des paramètres (#FF6E5C2A et #FF4E421F)
        private class DarkColorTable : ProfessionalColorTable
        {
            // Contour : #FF6E5C2A (RGB: 110, 92, 42)
            private static readonly System.Drawing.Color BorderColor = System.Drawing.Color.FromArgb(110, 92, 42);
            // Fond : #FFF6E7A9 (RGB: 246, 231, 169)
            private static readonly System.Drawing.Color BackgroundColor = System.Drawing.Color.FromArgb(246, 231, 169);
            
            public override System.Drawing.Color MenuBorder => BorderColor;
            public override System.Drawing.Color MenuItemBorder => BorderColor;
            // Couleur de survol : contour avec 15% d'opacité (RGB: 110, 92, 42 avec alpha 38 = 15% de 255)
            private static readonly System.Drawing.Color HoverColor = System.Drawing.Color.FromArgb(38, 110, 92, 42);
            
            public override System.Drawing.Color MenuItemSelected => HoverColor;
            public override System.Drawing.Color MenuItemSelectedGradientBegin => HoverColor;
            public override System.Drawing.Color MenuItemSelectedGradientEnd => HoverColor;
            public override System.Drawing.Color MenuItemPressedGradientBegin => BackgroundColor;
            public override System.Drawing.Color MenuItemPressedGradientEnd => BackgroundColor;
            public override System.Drawing.Color ToolStripDropDownBackground => BackgroundColor;
            public override System.Drawing.Color ImageMarginGradientBegin => BackgroundColor;
            public override System.Drawing.Color ImageMarginGradientMiddle => BackgroundColor;
            public override System.Drawing.Color ImageMarginGradientEnd => BackgroundColor;
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
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            
            System.Windows.Application.Current.Shutdown();
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
                System.Windows.MessageBox.Show(
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
                // Appliquer le nouveau style
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
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

        private void LoadConfiguration()
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

                }
                else
                {
                    config = new Config();
                    Logger.Info("MainWindow", "Fichier de configuration non trouvé, utilisation de la configuration par défaut");
                }
                
                // Créer ou ignorer les bulles principales selon la configuration persistée
                double centerX = SystemParameters.PrimaryScreenWidth / 2;
                double centerY = SystemParameters.PrimaryScreenHeight / 2;
                
                // Créer la bulle Kikimeter seulement si elle n'existe pas déjà
                // La bulle fait maintenant 60x180 (3 carrés empilés : Kikimeter, Loot, Paramètres)
                if (kikimeterBubble == null)
                {
                    CreateKikimeterBubble((int)centerX, (int)centerY + 100);
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
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur chargement config: {ex.Message}");
                config = new Config();
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
                if (kikimeterBubble.ContextMenu != null)
                {
                    kikimeterBubble.ContextMenu.Opened += (_, _) => NotifyContextMenuOpened();
                    kikimeterBubble.ContextMenu.Closed += (_, _) => NotifyContextMenuClosed();
                }
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
                        System.Windows.MessageBox.Show($"Erreur lors de l'ouverture du kikimeter: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        System.Windows.MessageBox.Show($"Erreur lors de l'ouverture du loot: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        System.Windows.MessageBox.Show($"Erreur lors de l'ouverture du navigateur web: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        System.Windows.MessageBox.Show($"Erreur lors de l'ouverture des paramètres: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    
                    System.Windows.MessageBox.Show(
                        "Le chemin a été configuré avec succès. La fenêtre Kikimeter sera redémarrée pour utiliser le nouveau chemin.",
                        "Configuration sauvegardée",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
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
                    
                    System.Windows.MessageBox.Show(
                        "Le chemin a été configuré avec succès.",
                        "Configuration sauvegardée",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
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
                        string chatLogPath = config.LootChatLogPath ?? "";
                        string kikimeterLogPath = config.KikimeterLogPath ?? "";
                        if (!string.IsNullOrEmpty(chatLogPath) && System.IO.File.Exists(chatLogPath))
                        {
                            lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                            Logger.Info("MainWindow", "LootWindow.StartWatching appelé dès la création de la fenêtre");
                            
                            // Initialiser le suivi des ventes
                            InitializeSaleTracker();
                        }
                        else
                        {
                            Logger.Info("MainWindow", "Chemin du log chat non configuré ou fichier introuvable - StartWatching non démarré");
                        }
                    }
                    else
                    {
                        // Si la fenêtre existe déjà mais n'est pas visible, s'assurer que StartWatching est actif
                        if (!lootWindow.IsVisible)
                        {
                            string chatLogPath = config.LootChatLogPath ?? "";
                            string kikimeterLogPath = config.KikimeterLogPath ?? "";
                            if (!string.IsNullOrEmpty(chatLogPath) && System.IO.File.Exists(chatLogPath))
                            {
                                // Vérifier si StartWatching n'a pas encore été appelé
                                try
                                {
                                    lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                                    Logger.Info("MainWindow", "LootWindow.StartWatching appelé pour une fenêtre existante non visible");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Info("MainWindow", $"StartWatching déjà actif ou erreur: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    lootWindow.Show();
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
                Logger.Error("MainWindow", $"Erreur ToggleLoot: {ex.Message}");
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
                Logger.Info("MainWindow", $"Changement de serveur détecté ({label}), réinitialisation des affichages.");
                
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
                
                // Réinitialiser le fichier de configuration des personnages
                LootWindow_ResetButton_ExtraHandler(sender, new RoutedEventArgs());
                
                // Réinitialiser l'ordre manuel des joueurs en supprimant le fichier de sauvegarde
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var manualOrderPath = Path.Combine(appData, "Amaliassistant", "kikimeter_manual_order.json");
                    if (File.Exists(manualOrderPath))
                    {
                        File.Delete(manualOrderPath);
                        Logger.Info("MainWindow", $"Fichier d'ordre manuel supprimé: {manualOrderPath}");
                    }
                    
                    // Si KikimeterWindow existe, réinitialiser aussi l'ordre en mémoire
                    if (kikimeterWindow != null)
                    {
                        kikimeterWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                // Réinitialiser l'ordre manuel dans KikimeterWindow
                                // On ne peut pas accéder directement aux champs privés, mais le fichier est déjà supprimé
                                // donc au prochain chargement, l'ordre sera réinitialisé
                                Logger.Info("MainWindow", "Fichier d'ordre manuel supprimé, KikimeterWindow le rechargera au prochain démarrage");
                            }
                            catch (Exception ex)
                            {
                                Logger.Info("MainWindow", $"Erreur lors de la notification à KikimeterWindow: {ex.Message}");
                            }
                        }), DispatcherPriority.Normal);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("MainWindow", $"Erreur lors de la suppression du fichier d'ordre manuel: {ex.Message}");
                }
                
                // Réinitialiser la liste des personnages et l'ordre dans SettingsWindow si elle existe
                if (settingsWindow != null)
                {
                    try
                    {
                        settingsWindow.ResetCharacterList();
                        settingsWindow.ResetPlayerOrder();
                        Logger.Info("MainWindow", "Liste des personnages et ordre réinitialisés dans SettingsWindow suite au changement de serveur.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors de la réinitialisation de SettingsWindow: {ex.Message}");
                    }
                }
                else
                {
                    // SettingsWindow n'existe pas encore - le reset des personnages a déjà été fait
                    // dans ResetAllLoot via ResetCharacterStorage, donc c'est OK
                    Logger.Info("MainWindow", "SettingsWindow n'existe pas encore, reset des personnages déjà effectué via LootWindow.ResetAllLoot");
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
                    lootWindow.Width = positions.LootWindow.Width;
                    lootWindow.Height = positions.LootWindow.Height;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur LoadLootWindowPosition: {ex.Message}");
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
        
        private void ToggleSettingsWindow()
        {
            try
            {
                if (settingsWindow == null || !settingsWindow.IsVisible)
                {
                    // Créer ou réouvrir la fenêtre Settings
                    if (settingsWindow == null)
                    {
                        // Récupérer les joueurs actuels depuis KikimeterWindow si elle existe
                        IEnumerable<string>? currentPlayers = null;
                        Func<IEnumerable<string>>? getCurrentPlayers = null;
                        
                        if (kikimeterWindow != null)
                        {
                            // Récupérer les joueurs actuels depuis KikimeterWindow
                            try
                            {
                                var playerStats = kikimeterWindow.PlayersCollection;
                                currentPlayers = playerStats.Select(p => p.Name).ToList();
                                getCurrentPlayers = () => kikimeterWindow.PlayersCollection.Select(p => p.Name);
                                Logger.Info("MainWindow", $"Récupération de {currentPlayers.Count()} joueurs pour SettingsWindow");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("MainWindow", $"Impossible de récupérer les joueurs actuels: {ex.Message}");
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
                                    lootWindow != null && 
                                    lootWindow.IsVisible)
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
                            () => LootWindow_ResetButton_ExtraHandler(null, new RoutedEventArgs())
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
                System.Windows.MessageBox.Show($"Erreur lors de l'ouverture du navigateur web: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (kikimeterWindow == null && !string.IsNullOrEmpty(config.KikimeterLogPath) && File.Exists(config.KikimeterLogPath))
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
                        
                        kikimeterWindow = new GameOverlay.Kikimeter.KikimeterWindow(config.KikimeterLogPath, individualMode);
                        kikimeterWindow.Visibility = Visibility.Hidden; // Créer mais cacher
                        kikimeterWindow.ShowInTaskbar = false;
                        
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
                if (lootWindow == null && !string.IsNullOrEmpty(config.LootChatLogPath) && File.Exists(config.LootChatLogPath))
                {
                    try
                    {
                        lootWindow = new GameOverlay.Kikimeter.Views.LootWindow();
                        lootWindow.Visibility = Visibility.Hidden; // Créer mais cacher
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
                        
                        // Démarrer la surveillance immédiatement
                        string chatLogPath = config.LootChatLogPath ?? "";
                        string kikimeterLogPath = config.KikimeterLogPath ?? "";
                        if (!string.IsNullOrEmpty(chatLogPath) && File.Exists(chatLogPath))
                        {
                            lootWindow.StartWatching(chatLogPath, kikimeterLogPath);
                            Logger.Info("MainWindow", "LootWindow créée en arrière-plan - StartWatching démarré");
                            
                            // Initialiser le suivi des ventes
                            InitializeSaleTracker();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("MainWindow", $"Erreur lors de l'initialisation de LootWindow en arrière-plan: {ex.Message}");
                    }
                }
                
                // Initialiser le SaleTracker même si la LootWindow n'est pas créée (si le chemin du log est configuré)
                if (_saleTracker == null && !string.IsNullOrEmpty(config.LootChatLogPath) && File.Exists(config.LootChatLogPath))
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
                System.Windows.MessageBox.Show($"Erreur sauvegarde: {ex.Message}");
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.ContextMenu.IsOpen = true;
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


        public void HideOverlay_Click(object sender, RoutedEventArgs e)
        {
            ToggleOverlay();
        }
        
        public void TestSaleNotification_Click(object sender, RoutedEventArgs e)
        {
            TestSaleNotification();
        }

        public void Exit_Click(object sender, RoutedEventArgs e)
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
                System.Windows.MessageBox.Show($"Erreur lors de la sélection de couleur : {ex.Message}",
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
                        notificationWindow.Topmost = true;
                        notificationWindow.Topmost = false;
                        notificationWindow.Topmost = true;
                        
                        // Activer la fenêtre pour s'assurer qu'elle est visible même pendant les jeux en plein écran
                        notificationWindow.Activate();
                        
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
                if (string.IsNullOrWhiteSpace(chatLogPath) || !File.Exists(chatLogPath))
                {
                    Logger.Debug("MainWindow", "Chemin du log de chat non configuré ou fichier inexistant, SaleTracker non initialisé");
                    return;
                }
                
                _saleTracker = new GameOverlay.Kikimeter.Services.SaleTracker(chatLogPath);
                _saleTracker.SaleDetected += SaleTracker_SaleDetected;
                
                // Créer et démarrer le timer pour la lecture périodique
                // Interval réduit à 25ms pour une détection plus rapide et ne rien rater
                _saleTrackerTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(25)
                };
                _saleTrackerTimer.Tick += SaleTrackerTimer_Tick;
                _saleTrackerTimer.Start();
                
                Logger.Info("MainWindow", $"SaleTracker initialisé pour la détection des ventes en temps réel (fichier: {chatLogPath})");
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
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow", $"Erreur dans SaleTrackerTimer_Tick: {ex.Message}");
            }
        }
        
        private void SaleTracker_SaleDetected(object? sender, SaleInfo saleInfo)
        {
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



