using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using GameOverlay.Kikimeter;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Models;
using GameOverlay.Themes;
using Newtonsoft.Json;
using System.Windows.Forms;
using FormsColorDialog = System.Windows.Forms.ColorDialog;

namespace GameOverlay.Kikimeter.Views;

/// <summary>
/// Fenêtre individuelle pour afficher les statistiques d'un joueur
/// </summary>
public partial class PlayerWindow : Window
{
    private readonly PlayerStats _playerStats;
    private readonly object? _parentWindow;
    private bool _isDragging = false;
    private WpfPoint _dragStartPoint;
    private double _maxDamage = PlayerStats.MappedMaxValue;
    private double _maxDamageTaken = PlayerStats.MappedMaxValue;
    private double _maxHealing = PlayerStats.MappedMaxValue;
    private double _maxShield = PlayerStats.MappedMaxValue;
    
    public string PlayerName => _playerStats?.Name ?? "Joueur";
    public string DPTDisplay => $"DPT: {_playerStats?.DamagePerTurn ?? 0}";

    public PlayerWindow(PlayerStats playerStats, double maxDamage = 0d, double maxDamageTaken = 0d, double maxHealing = 0d, double maxShield = 0d, object? parentWindow = null)
    {
        InitializeComponent();
        
        _playerStats = playerStats;
        _parentWindow = parentWindow;
        _maxDamage = maxDamage > 0d ? maxDamage : PlayerStats.MappedMaxValue;
        _maxDamageTaken = maxDamageTaken > 0d ? maxDamageTaken : PlayerStats.MappedMaxValue;
        _maxHealing = maxHealing > 0d ? maxHealing : PlayerStats.MappedMaxValue;
        _maxShield = maxShield > 0d ? maxShield : PlayerStats.MappedMaxValue;
        
        // Définir le DataContext pour les bindings
        DataContext = this;
        
        // S'abonner aux changements du joueur
        _playerStats.PropertyChanged += PlayerStats_PropertyChanged;
        
        // Initialiser l'affichage
        UpdateUI();
        
        // Activer le drag & redimensionnement
        this.SourceInitialized += PlayerWindow_SourceInitialized;
        
        // Définir le titre de la fenêtre
        this.Title = "📊";
        
        // Synchroniser avec le thème
        UpdateAccentBrushResource();
        ThemeManager.AccentColorChanged += (s, args) =>
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateAccentBrushResource()));
        };
        
        // Charger la couleur de fond sauvegardée
        Loaded += (s, e) => 
        {
            LoadPlayerWindowBackgroundColor();
            LoadPlayerWindowBackground();
            // Ajouter des gestionnaires d'événements pour retourner le focus au jeu après chaque interaction
            this.MouseDown += (sender, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.MouseUp += (sender, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.KeyDown += (sender, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.KeyUp += (sender, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.PreviewMouseDown += (sender, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.PreviewMouseUp += (sender, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
        };
        
        Logger.Info("PlayerWindow", $"PlayerWindow créé pour {_playerStats?.Name}");
    }
    
    private void UpdateAccentBrushResource()
    {
        try
        {
            SolidColorBrush accentBrush = ThemeManager.AccentBrush;
            Resources["CyanAccentBrush"] = accentBrush;
        }
        catch { }
    }
    
    private void PlayerWindow_SourceInitialized(object? sender, EventArgs e)
    {
        System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
        System.Windows.Interop.HwndSource? source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WindowProc);
        
        // Exclure la fenêtre d'Alt+Tab
        ExcludeFromAltTab(helper.Handle);
    }
    
    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;
        const int HTTRANSPARENT = -1;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;
        const int GRIP_SIZE = 5;
        
        if (msg == WM_NCHITTEST)
        {
            WpfPoint pt = new WpfPoint((int)lParam & 0xFFFF, (int)lParam >> 16);
            WpfPoint windowPt = this.PointFromScreen(pt);
            
            Rect rect = new Rect(this.RenderSize);
            
            // Vérifier d'abord les zones de redimensionnement
            // Coins
            if (windowPt.X <= GRIP_SIZE && windowPt.Y <= GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTTOPLEFT);
            }
            else if (windowPt.X >= rect.Width - GRIP_SIZE && windowPt.Y <= GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTTOPRIGHT);
            }
            else if (windowPt.X <= GRIP_SIZE && windowPt.Y >= rect.Height - GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTBOTTOMLEFT);
            }
            else if (windowPt.X >= rect.Width - GRIP_SIZE && windowPt.Y >= rect.Height - GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTBOTTOMRIGHT);
            }
            // Bords
            else if (windowPt.X <= GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTLEFT);
            }
            else if (windowPt.X >= rect.Width - GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTRIGHT);
            }
            else if (windowPt.Y <= GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTTOP);
            }
            else if (windowPt.Y >= rect.Height - GRIP_SIZE)
            {
                handled = true;
                return new IntPtr(HTBOTTOM);
            }
            
            // Vérifier si on clique sur un élément interactif
            var hitTestResult = System.Windows.Media.VisualTreeHelper.HitTest(this, windowPt);
            
            if (hitTestResult != null)
            {
                var visualHit = hitTestResult.VisualHit;
                var current = visualHit as System.Windows.DependencyObject;
                
                // Vérifier si on est sur un élément interactif
                while (current != null && current != this && current != MainBorder)
                {
                    if (current is System.Windows.Controls.Button ||
                        current is System.Windows.Controls.ProgressBar ||
                        current is System.Windows.Controls.CheckBox)
                    {
                        // Élément interactif trouvé - cliquable
                        return IntPtr.Zero;
                    }
                    
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                }
            }
            
            // Sinon, permettre le drag de la fenêtre via HTCLIENT
            handled = true;
            return new IntPtr(HTCLIENT);
        }
        
        return IntPtr.Zero;
    }
    
    /// <summary>
    /// Applique la couleur de fond aux sections (rectangles cyan)
    /// PlayerWindow n'a pas de rectangles cyan, mais cette méthode est appelée pour compatibilité
    /// </summary>
    public void ApplySectionBackgroundColor(string colorHex)
    {
        try
        {
            if (!string.IsNullOrEmpty(colorHex))
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                // Pas de transparence pour le mode individuel - garder la couleur opaque
                if (color.A != 255)
                {
                    color.A = 255; // Forcer l'opacité complète
                }
                var brush = new SolidColorBrush(color);
                Resources["SectionBackgroundBrush"] = brush;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur ApplySectionBackgroundColor: {ex.Message}");
        }
    }
    
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
    
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    
    private void ExcludeFromAltTab(IntPtr hwnd)
    {
        try
        {
            uint extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            extendedStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
        }
        catch (Exception ex)
        {
            Logger.Debug("PlayerWindow", $"Erreur ExcludeFromAltTab: {ex.Message}");
        }
    }
    
    
    private void PlayerStats_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Mettre à jour l'UI quand les stats changent
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            try
            {
                UpdateUI();
            }
            catch (Exception ex)
            {
                Logger.Error("PlayerWindow", $"Erreur PlayerStats_PropertyChanged: {ex.Message}");
            }
        }));
    }
    
    public void UpdateUI()
    {
        try
        {
            if (_playerStats == null) return;
            
            // Mettre à jour les valeurs affichées
            DamageText.Text = _playerStats.DamageDealt.ToString();
            ReceivedText.Text = _playerStats.DamageTaken.ToString();
            HealingText.Text = _playerStats.HealingDone.ToString();
            ShieldText.Text = _playerStats.ShieldGiven.ToString();
            // Tours retirés - le jeu les affiche déjà
            
            // Mettre à jour TurnsText : afficher le nom du joueur ou "En attente..."
            var name = PlayerName;
            var hasRealPlayerName = _playerStats != null && !string.IsNullOrWhiteSpace(_playerStats.Name) && name != "En attente..." && name != "Joueur";
            
            if (TurnsText != null)
            {
                if (hasRealPlayerName)
                {
                    // En combat avec un vrai nom → afficher le nom du joueur
                    TurnsText.Text = name;
                    TurnsText.FontWeight = System.Windows.FontWeights.Bold;
                }
                else
                {
                    // Pas de combat → afficher "En attente..."
                    TurnsText.Text = "En attente...";
                    TurnsText.FontWeight = System.Windows.FontWeights.Normal;
                }
            }
            
            if (DPTText != null)
            {
                DPTText.Text = DPTDisplay;
            }
            
            // Mettre à jour les barres de progression
            if (_playerStats != null)
            {
                DamageProgressBar.Value = _playerStats.DamageDealtRatio;
            DamageProgressBar.Maximum = 1;
            
                ReceivedProgressBar.Value = _playerStats.DamageTakenRatio;
            ReceivedProgressBar.Maximum = 1;
            
                HealingProgressBar.Value = _playerStats.HealingDoneRatio;
            HealingProgressBar.Maximum = 1;
            
                ShieldProgressBar.Value = _playerStats.ShieldGivenRatio;
            ShieldProgressBar.Maximum = 1;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur UpdateUI: {ex.Message}");
        }
    }
    
    public void UpdateMaxValues(double maxDamage, double maxDamageTaken, double maxHealing, double maxShield)
    {
        _maxDamage = 1d;
        _maxDamageTaken = 1d;
        _maxHealing = 1d;
        _maxShield = 1d;
        UpdateUI();
    }
    
    public void SetZoom(double zoomValue)
    {
        if (WindowScale != null)
        {
            WindowScale.ScaleX = zoomValue;
            WindowScale.ScaleY = zoomValue;
            Logger.Debug("PlayerWindow", $"Zoom modifié: {(int)(zoomValue * 100)}%");
        }
    }
    
    #region Drag & Drop
    
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            Mouse.Capture(this);
        }
    }
    
    private void Window_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            WpfPoint currentPosition = e.GetPosition(this);
            Vector offset = currentPosition - _dragStartPoint;
            
            Left += offset.X;
            Top += offset.Y;
        }
    }
    
    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            // Retourner le focus au jeu après déplacement
            ReturnFocusToGame();
        }
    }
    
    private void ReturnFocusToGame()
    {
        // Utiliser un timer pour s'assurer que l'interaction est terminée
        var timer = new DispatcherTimer
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
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("PlayerWindow", $"Erreur lors du retour du focus: {ex.Message}");
            }
        };
        
        timer.Start();
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    #endregion
    
    private bool _isMinimized = false;
    private double _savedHeight = 0;
    
    private System.Windows.Controls.Grid? GetMainGrid()
    {
        return this.FindName("MainGrid") as System.Windows.Controls.Grid;
    }
    
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isMinimized)
        {
            // Restaurer la fenêtre : remettre la hauteur originale
            Height = _savedHeight;
            
            // Réafficher le contenu principal
            var mainGrid = GetMainGrid();
            if (mainGrid != null && mainGrid.RowDefinitions.Count > 1)
            {
                mainGrid.RowDefinitions[1].Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
            }
            
            _isMinimized = false;
            var minimizeBtn = this.FindName("MinimizeButton") as System.Windows.Controls.Button;
            if (minimizeBtn != null)
            {
                minimizeBtn.Content = "─";
            }
            Logger.Debug("PlayerWindow", $"Fenêtre restaurée pour {_playerStats?.Name ?? "inconnu"}");
        }
        else
        {
            // Minimiser : réduire la fenêtre à juste la barre de titre
            // Sauvegarder la hauteur actuelle
            _savedHeight = Height;
            
            // Réduire la hauteur à juste la barre de titre (environ 50 pixels)
            Height = 50;
            
            // Cacher le contenu principal (Row 1)
            var mainGrid = GetMainGrid();
            if (mainGrid != null && mainGrid.RowDefinitions.Count > 1)
            {
                mainGrid.RowDefinitions[1].Height = new System.Windows.GridLength(0);
            }
            
            _isMinimized = true;
            var minimizeBtn = this.FindName("MinimizeButton") as System.Windows.Controls.Button;
            if (minimizeBtn != null)
            {
                minimizeBtn.Content = "□";
            }
            Logger.Debug("PlayerWindow", $"Fenêtre minimisée pour {_playerStats?.Name ?? "inconnu"}");
            ReturnFocusToGame();
        }
    }
    
    private void BackToNormalModeCheckbox_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Si la checkbox est cochée, revenir au mode normal
            var checkbox = sender as System.Windows.Controls.CheckBox;
            if (checkbox != null && checkbox.IsChecked == true)
            {
                // Chercher la fenêtre Kikimeter principale pour appeler ToggleToNormalMode
                var kikimeterWindow = System.Windows.Application.Current.Windows
                    .OfType<GameOverlay.Kikimeter.KikimeterWindow>()
                    .FirstOrDefault();
                
                if (kikimeterWindow != null)
                {
                    kikimeterWindow.ToggleToNormalMode();
                    Logger.Info("PlayerWindow", "Retour au mode normal depuis PlayerWindow");
                }
                
                // Fermer cette fenêtre individuelle
                Close();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur dans BackToNormalModeCheckbox_Click: {ex.Message}");
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Logger.Debug("PlayerWindow", $"Bouton Fermer cliqué - fermeture de la fenêtre {_playerStats?.Name ?? "inconnue"}");
        Close();
        
        // Redonner le focus au jeu après avoir fermé
        ReturnFocusToGame();
    }
    
    private void ReturnToMainCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            // Chercher la fenêtre Kikimeter principale pour revenir au mode normal
            var kikimeterWindow = System.Windows.Application.Current.Windows
                .OfType<GameOverlay.Kikimeter.KikimeterWindow>()
                .FirstOrDefault();
            
            if (kikimeterWindow != null)
            {
                // Fermer toutes les fenêtres individuelles et revenir au mode normal
                kikimeterWindow.CloseAllIndividualWindows();
                kikimeterWindow.SetIndividualMode(false, suppressEvent: true);
                kikimeterWindow.Show();
                Logger.Info("PlayerWindow", "Retour au mode normal depuis PlayerWindow");
            }
            
            // Fermer cette fenêtre individuelle
            Close();
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur lors du retour à la fenêtre principale: {ex.Message}");
        }
    }
    
    private void ReturnToMainCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        // Ne rien faire lors de la décochage
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Désabonner de PropertyChanged pour éviter les fuites mémoire
        if (_playerStats != null)
        {
            _playerStats.PropertyChanged -= PlayerStats_PropertyChanged;
        }
        base.OnClosed(e);
    }
    
    private void MainBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CreateContextMenu().IsOpen = true;
    }
    
    private ContextMenu CreateContextMenu()
    {
        var contextMenu = new ContextMenu();
        
        // Menu pour changer la couleur de fond de la section
        var sectionBackgroundColorMenuItem = new MenuItem { Header = "🎨 Couleur de fond de la section" };
        
        // Couleurs prédéfinies
        var backgroundColors = new[]
        {
            ("Noir (défaut)", "#FF000000"),
            ("Gris très foncé", "#FF0F0F0F"),
            ("Gris foncé", "#FF1A1A1A"),
            ("Gris moyen", "#FF2A2A2A"),
            ("Gris clair", "#FF404040"),
            ("Bleu foncé", "#FF1A2A4A"),
            ("Violet foncé", "#FF2A1A4A"),
            ("Vert foncé", "#FF1A4A2A"),
            ("Rouge foncé", "#FF4A1A1A")
        };
        
        foreach (var (name, hex) in backgroundColors)
        {
            var colorItem = new MenuItem { Header = name };
            
            // Afficher un carré de couleur
            var colorBox = new Border
            {
                Width = 16,
                Height = 16,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var stackPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(colorBox);
            stackPanel.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            colorItem.Header = stackPanel;
            
            colorItem.Click += (s, e) =>
            {
                ApplyPlayerWindowBackgroundColor(hex);
                SavePlayerWindowBackgroundColor(hex);
            };
            
            sectionBackgroundColorMenuItem.Items.Add(colorItem);
        }
        
        sectionBackgroundColorMenuItem.Items.Add(new Separator());
        
        // Couleur personnalisée
        var customColorItem = new MenuItem { Header = "🎨 Couleur personnalisée..." };
        customColorItem.Click += (s, e) => OpenPlayerWindowBackgroundColorPicker();
        sectionBackgroundColorMenuItem.Items.Add(customColorItem);
        
        // Réinitialiser
        var resetColorItem = new MenuItem { Header = "🔄 Réinitialiser" };
        resetColorItem.Click += (s, e) =>
        {
            ApplyPlayerWindowBackgroundColor("#FF000000");
            SavePlayerWindowBackgroundColor("#FF000000");
        };
        sectionBackgroundColorMenuItem.Items.Add(resetColorItem);
        
        contextMenu.Items.Add(sectionBackgroundColorMenuItem);
        
        // Menu pour changer la couleur de fond de la fenêtre
        var windowBackgroundColorMenuItem = new MenuItem { Header = "🎨 Couleur de fond de la fenêtre" };
        
        // Option pour activer/désactiver le fond
        var enableBackgroundItem = new MenuItem { Header = "Activer le fond" };
        enableBackgroundItem.IsCheckable = true;
        string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
        if (File.Exists(configFile))
        {
            try
            {
                var json = File.ReadAllText(configFile);
                var config = JsonConvert.DeserializeObject<Config>(json);
                if (config != null)
                {
                    enableBackgroundItem.IsChecked = config.PlayerWindowBackgroundEnabled;
                }
            }
            catch { }
        }
        enableBackgroundItem.Click += (s, e) =>
        {
            var menuItem = s as MenuItem;
            if (menuItem != null)
            {
                SavePlayerWindowBackgroundEnabled(menuItem.IsChecked);
                LoadPlayerWindowBackground();
            }
        };
        windowBackgroundColorMenuItem.Items.Add(enableBackgroundItem);
        
        windowBackgroundColorMenuItem.Items.Add(new Separator());
        
        // Couleurs prédéfinies pour le fond de fenêtre
        foreach (var (name, hex) in backgroundColors)
        {
            var colorItem = new MenuItem { Header = name };
            
            var colorBox = new Border
            {
                Width = 16,
                Height = 16,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var stackPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(colorBox);
            stackPanel.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            colorItem.Header = stackPanel;
            
            colorItem.Click += (s, e) =>
            {
                SavePlayerWindowBackgroundColorSetting(hex);
                LoadPlayerWindowBackground();
            };
            
            windowBackgroundColorMenuItem.Items.Add(colorItem);
        }
        
        windowBackgroundColorMenuItem.Items.Add(new Separator());
        
        // Couleur personnalisée pour le fond de fenêtre
        var customWindowColorItem = new MenuItem { Header = "🎨 Couleur personnalisée..." };
        customWindowColorItem.Click += (s, e) => OpenPlayerWindowBackgroundColorPickerForWindow();
        windowBackgroundColorMenuItem.Items.Add(customWindowColorItem);
        
        contextMenu.Items.Add(windowBackgroundColorMenuItem);

        // Synchroniser le thème sur le menu contextuel
        SyncPlayerWindowContextMenuTheme(contextMenu);
        
        // S'abonner aux changements de thème
        GameOverlay.Themes.ThemeManager.AccentColorChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(new Action(() => SyncPlayerWindowContextMenuTheme(contextMenu)));
        };
        GameOverlay.Themes.ThemeManager.BubbleBackgroundColorChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(new Action(() => SyncPlayerWindowContextMenuTheme(contextMenu)));
        };
        
        return contextMenu;
    }
    
    private void SyncPlayerWindowContextMenuTheme(ContextMenu contextMenu)
    {
        try
        {
            GameOverlay.Themes.ThemeManager.ApplyContextMenuTheme(contextMenu);

            if (contextMenu == null)
            {
                return;
            }

            foreach (var item in contextMenu.Items.OfType<MenuItem>())
            {
                // Synchroniser les TextBlocks dans les Headers qui sont des StackPanel
                if (item.Header is StackPanel stackPanel)
                {
                    foreach (var child in stackPanel.Children.OfType<TextBlock>())
                    {
                        child.Foreground = new System.Windows.Media.SolidColorBrush(GameOverlay.Themes.ThemeManager.AccentColor);
                    }
                }
                
                // Synchroniser les sous-menus
                foreach (var subItem in item.Items.OfType<MenuItem>())
                {
                    if (subItem.Header is StackPanel subStackPanel)
                    {
                        foreach (var child in subStackPanel.Children.OfType<TextBlock>())
                        {
                            child.Foreground = new System.Windows.Media.SolidColorBrush(GameOverlay.Themes.ThemeManager.AccentColor);
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    private void ApplyPlayerWindowBackgroundColor(string colorHex)
    {
        try
        {
            // Utiliser la même méthode que ApplySectionBackgroundColor pour la synchronisation
            // Cela garantit que la ressource dynamique SectionBackgroundBrush est mise à jour
            ApplySectionBackgroundColor(colorHex);
            
            // Sauvegarder aussi dans PlayerWindowBackgroundBrush pour compatibilité
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
            // Pas de transparence pour le mode individuel - garder la couleur opaque
            if (color.A != 255)
            {
                color.A = 255; // Forcer l'opacité complète
            }
            var brush = new SolidColorBrush(color);
            
            if (!Resources.Contains("PlayerWindowBackgroundBrush"))
            {
                Resources.Add("PlayerWindowBackgroundBrush", brush);
            }
            else
            {
                Resources["PlayerWindowBackgroundBrush"] = brush;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur ApplyPlayerWindowBackgroundColor: {ex.Message}");
        }
    }
    
    private void SavePlayerWindowBackgroundColor(string colorHex)
    {
        try
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                var config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                config.PlayerWindowSectionBackgroundColor = colorHex;
                
                var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFile, updatedJson);
                
                Logger.Info("PlayerWindow", $"Couleur de fond sauvegardée: {colorHex}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur SavePlayerWindowBackgroundColor: {ex.Message}");
        }
    }
    
    private void OpenPlayerWindowBackgroundColorPicker()
    {
        try
        {
            using (var colorDialog = new FormsColorDialog())
            {
                // Récupérer la couleur actuelle
                string currentColorHex = LoadPlayerWindowBackgroundColor();
                var currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColorHex);
                
                colorDialog.Color = System.Drawing.Color.FromArgb(
                    currentColor.A,
                    currentColor.R,
                    currentColor.G,
                    currentColor.B);
                
                colorDialog.FullOpen = true;
                colorDialog.AllowFullOpen = true;
                
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedColor = colorDialog.Color;
                    string hexColor = $"#{selectedColor.A:X2}{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
                    
                    ApplyPlayerWindowBackgroundColor(hexColor);
                    SavePlayerWindowBackgroundColor(hexColor);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur OpenPlayerWindowBackgroundColorPicker: {ex.Message}");
        }
    }
    
    private string LoadPlayerWindowBackgroundColor()
    {
        try
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                var config = JsonConvert.DeserializeObject<Config>(json);
                if (config != null && !string.IsNullOrEmpty(config.PlayerWindowSectionBackgroundColor))
                {
                    ApplyPlayerWindowBackgroundColor(config.PlayerWindowSectionBackgroundColor);
                    return config.PlayerWindowSectionBackgroundColor;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur LoadPlayerWindowBackgroundColor: {ex.Message}");
        }
        return "#FF000000";
    }
    
    private void LoadPlayerWindowBackground()
    {
        try
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                var config = JsonConvert.DeserializeObject<Config>(json);
                if (config != null)
                {
                    // Appliquer le background de fenêtre si activé
                    if (config.PlayerWindowBackgroundEnabled)
                    {
                        try
                        {
                            var bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(config.PlayerWindowBackgroundColor);
                            bgColor.A = (byte)(config.PlayerWindowBackgroundOpacity * 255);
                            this.Background = new System.Windows.Media.SolidColorBrush(bgColor);
                            // Si le background de fenêtre est activé, appliquer aussi au MainBorder
                            if (MainBorder != null)
                            {
                                MainBorder.Background = new System.Windows.Media.SolidColorBrush(bgColor);
                            }
                            Logger.Info("PlayerWindow", $"Background de fenêtre appliqué: {config.PlayerWindowBackgroundColor} avec opacité {config.PlayerWindowBackgroundOpacity}");
                        }
                        catch { }
                    }
                    else
                    {
                        this.Background = System.Windows.Media.Brushes.Transparent;
                        // S'assurer que le MainBorder reste transparent
                        if (MainBorder != null)
                        {
                            MainBorder.Background = System.Windows.Media.Brushes.Transparent;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur LoadPlayerWindowBackground: {ex.Message}");
        }
    }
    
    private void SavePlayerWindowBackgroundEnabled(bool enabled)
    {
        try
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
            Config config;
            
            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                config = new Config();
            }
            
            config.PlayerWindowBackgroundEnabled = enabled;
            
            var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFile, updatedJson);
            
            Logger.Info("PlayerWindow", $"Background de fenêtre {(enabled ? "activé" : "désactivé")}");
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur SavePlayerWindowBackgroundEnabled: {ex.Message}");
        }
    }
    
    private void SavePlayerWindowBackgroundColorSetting(string colorHex)
    {
        try
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
            Config config;
            
            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                config = new Config();
            }
            
            config.PlayerWindowBackgroundColor = colorHex;
            config.PlayerWindowBackgroundEnabled = true; // Activer automatiquement quand on choisit une couleur
            
            var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFile, updatedJson);
            
            Logger.Info("PlayerWindow", $"Couleur de fond de fenêtre sauvegardée: {colorHex}");
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur SavePlayerWindowBackgroundColorSetting: {ex.Message}");
        }
    }
    
    private void OpenPlayerWindowBackgroundColorPickerForWindow()
    {
        try
        {
            using (var colorDialog = new System.Windows.Forms.ColorDialog())
            {
                colorDialog.FullOpen = true;
                string currentColorHex = "#FF000000";
                string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
                if (File.Exists(configFile))
                {
                    try
                    {
                        var json = File.ReadAllText(configFile);
                        var config = JsonConvert.DeserializeObject<Config>(json);
                        if (config != null && !string.IsNullOrEmpty(config.PlayerWindowBackgroundColor))
                        {
                            currentColorHex = config.PlayerWindowBackgroundColor;
                        }
                    }
                    catch { }
                }
                
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColorHex);
                colorDialog.Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string hexColor = $"#FF{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
                    SavePlayerWindowBackgroundColorSetting(hexColor);
                    LoadPlayerWindowBackground();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("PlayerWindow", $"Erreur OpenPlayerWindowBackgroundColorPickerForWindow: {ex.Message}");
        }
    }
}


