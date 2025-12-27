using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ShapesPath = System.Windows.Shapes.Path;
using GameOverlay.Models;
using GameOverlay.Themes;
using Newtonsoft.Json;
using WpfPoint = System.Windows.Point;

namespace GameOverlay.Windows;

public partial class KikimeterBubble : UserControl
{
    // Fenêtre gérée par MainWindow via l'événement OnOpenKikimeter
    private string _logPath;
    private KikimeterIndividualMode _individualMode;
    private Config? _config;
    private double _bubbleX;
    private double _bubbleY;
    private double _opacity;
    
    // Événements
    public event EventHandler? OnOpenKikimeter;
    public event EventHandler? OnOpenLoot;
    public event EventHandler? OnOpenWeb;
    public event EventHandler? OnOpenSettings;
    public event EventHandler? OnConfigurePath;
    public event EventHandler<System.Windows.Point>? PositionChanged;
    public event EventHandler<double>? SizeChanged;
    public event EventHandler<double>? OpacityChanged;
    public event EventHandler<double>? ZoomChanged;
    public event EventHandler? DeleteRequested;

    public KikimeterBubble(string logPath, KikimeterIndividualMode individualMode, Config config, double x, double y, double opacity, double size)
    {
        InitializeComponent();
        _logPath = logPath;
        _individualMode = individualMode;
        _config = config;
        _bubbleX = x;
        _bubbleY = y;
        _opacity = opacity;
        UpdateSize(size);
        UpdateOpacity(opacity);
        
        // Plus besoin d'appliquer le fond avec la couleur de config
        // On utilise maintenant l'image de fond définie dans le XAML
        
        // Le déplacement se fait uniquement via la croix (DragHandle), pas sur toute la bulle
        // Créer un menu contextuel explicite avec style forcé pour éviter le cyan
        CreateContextMenu();
        
        // Synchroniser avec le thème (couleur d'accent)
        UpdateAccentBrushResource();
        ThemeManager.AccentColorChanged += (s, args) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateAccentBrushResource();
            }));
        };
        
        // Plus besoin de synchroniser avec le thème (couleur de fond)
        // On utilise maintenant l'image de fond définie dans le XAML
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
    
    private void CreateContextMenu()
    {
        // Créer un menu contextuel vide (on utilisera celui du MainWindow via l'événement)
        // Mais on s'assure qu'il ne bloque pas l'héritage du menu parent
        // On va plutôt intercepter l'ouverture et forcer les couleurs
        this.ContextMenu = null; // Permettre l'héritage du menu du MainWindow
    }
    
    public void UpdateSize(double size)
    {
        try
        {
            if (size < 10 || size > 200) return; // Validation de la taille
            
            // La hauteur est 4x 40px pour les quatre carrés empilés + 3x 4px d'espacement (Kikimeter, Loot, Web, Paramètres)
            // La largeur inclut le carré (40px) + la colonne de la croix (30px) = 70px
            double totalWidth = 40 + 30; // 40px carré + 30px colonne croix
            double totalHeight = 40 * 4 + 4 * 3; // 4 carrés de 40px + 3 espaces de 4px = 172px
            Width = totalWidth;
            Height = totalHeight;
            
            // Mettre à jour la taille des trois carrés principaux (40x40 chacun)
            if (TopSquare != null)
                {
                TopSquare.Width = 40;
                TopSquare.Height = 40;
            }
            if (MiddleSquare != null)
                        {
                MiddleSquare.Width = 40;
                MiddleSquare.Height = 40;
                        }
            if (BottomSquare != null)
            {
                BottomSquare.Width = 40;
                BottomSquare.Height = 40;
            }
            
            // Mettre à jour la taille du carré de déplacement (25x25px)
            if (DragHandle != null)
                        {
                DragHandle.Width = 25;
                DragHandle.Height = 25;
                        }
            
            // Mettre à jour la taille des carrés (chaque carré = size x size)
            // Les RowDefinitions sont gérées automatiquement par le Grid
            
            SizeChanged?.Invoke(this, size);
            SavePosition();
        }
        catch (Exception ex)
        {
            try
            {
                Logger.Error("KikimeterBubble", $"Erreur dans UpdateSize: {ex.Message}");
            }
            catch { }
        }
    }
    
    public void UpdateOpacity(double opacity)
    {
        try
        {
            if (opacity < 0 || opacity > 1) return; // Validation de l'opacité
            
            _opacity = opacity;
            Opacity = opacity;
            // L'opacité est gérée par UpdateBackgroundWithOpacity pour les deux carrés
            OpacityChanged?.Invoke(this, opacity);
            SavePosition();
        }
        catch (Exception ex)
        {
            try
            {
                Logger.Error("KikimeterBubble", $"Erreur dans UpdateOpacity: {ex.Message}");
            }
            catch { }
        }
    }
    
    public void UpdateBackgroundWithOpacity(double opacity, string backgroundColorHex)
    {
        try
        {
            // Ne plus définir le Background car on utilise maintenant une image de fond
            // L'ImageBrush est défini dans le XAML et ne doit pas être écrasé
            
            // L'opacité est toujours gérée pour l'opacité globale du UserControl
            Opacity = opacity;
            SavePosition();
        }
        catch { }
    }
    
    // Gestionnaires d'événements pour le déplacement via la croix
    private bool _isDragging = false;
    private WpfPoint _dragStartPosition;
    private WpfPoint _dragStartPoint;
    
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Le DragHandle est maintenant une Image, pas un Border
        if (sender is FrameworkElement dragHandle)
        {
            _isDragging = true;
            _dragStartPosition = e.GetPosition(null); // Position relative à la fenêtre pour plus de fluidité
            
            // Obtenir la position de la bulle sur le Canvas parent
            var parentCanvas = this.Parent as System.Windows.Controls.Canvas;
            if (parentCanvas != null)
            {
                _dragStartPoint = new WpfPoint(
                    System.Windows.Controls.Canvas.GetLeft(this),
                    System.Windows.Controls.Canvas.GetTop(this));
            }
            
            dragHandle.CaptureMouse();
            e.Handled = true;
        }
    }
    
    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && sender is FrameworkElement dragHandle && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(null); // Position relative à la fenêtre pour plus de fluidité
            var delta = currentPosition - _dragStartPosition;
            
            // Déplacer la bulle
            var parentCanvas = this.Parent as System.Windows.Controls.Canvas;
            if (parentCanvas != null)
            {
                var newX = _dragStartPoint.X + delta.X;
                var newY = _dragStartPoint.Y + delta.Y;
                
                // Limiter aux bounds de l'écran
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                newX = Math.Max(0, Math.Min(newX, screenWidth - this.Width));
                newY = Math.Max(0, Math.Min(newY, screenHeight - this.Height));
                
                System.Windows.Controls.Canvas.SetLeft(this, newX);
                System.Windows.Controls.Canvas.SetTop(this, newY);
                
                _bubbleX = newX;
                _bubbleY = newY;
                
                PositionChanged?.Invoke(this, new System.Windows.Point(newX, newY));
            }
        }
    }
    
    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && sender is FrameworkElement dragHandle)
        {
            _isDragging = false;
            dragHandle.ReleaseMouseCapture();
            this.UseLayoutRounding = true; // Réactiver les arrondis
            SavePosition();
            e.Handled = true;
        }
    }
    
    private void SetSize(double size)
    {
        UpdateSize(size);
    }
    
    private void SetOpacity(double opacity)
    {
        UpdateOpacity(opacity);
    }

    private void SavePosition()
    {
        try
        {
            var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (File.Exists(configPath) && _config != null)
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                config.KikimeterBubbleX = (int)_bubbleX;
                config.KikimeterBubbleY = (int)_bubbleY;
                config.KikimeterBubbleOpacity = Opacity;
                config.KikimeterBubbleSize = Width;
                var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, updatedJson);
                try
                {
                    Logger.Debug("KikimeterBubble", $"Kikimeter bubble settings saved: Size={Width}, Opacity={Opacity}");
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            // Ne pas logger ici pour éviter les boucles infinies si Logger échoue
            // L'erreur est silencieuse car la sauvegarde n'est pas critique
        }
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        // Effet de survol optionnel sur les carrés
        if (TopSquare != null)
        {
            TopSquare.Opacity = Math.Min(1.0, Opacity + 0.1);
        }
        if (MiddleSquare != null)
        {
            MiddleSquare.Opacity = Math.Min(1.0, Opacity + 0.1);
        }
        if (WebSquare != null)
        {
            WebSquare.Opacity = Math.Min(1.0, Opacity + 0.1);
        }
        if (BottomSquare != null)
        {
            BottomSquare.Opacity = Math.Min(1.0, Opacity + 0.1);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        // Restaurer l'opacité normale
        if (TopSquare != null)
        {
            TopSquare.Opacity = 1.0;
        }
        if (MiddleSquare != null)
        {
            MiddleSquare.Opacity = 1.0;
        }
        if (WebSquare != null)
        {
            WebSquare.Opacity = 1.0;
        }
        if (BottomSquare != null)
        {
            BottomSquare.Opacity = 1.0;
        }
    }

    // Gestion du clic sur le carré supérieur pour toggle KikimeterWindow
    private bool _topSquareClicked = false;
    private WpfPoint _topSquareMouseDownPosition;
    
    private void TopSquare_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _topSquareClicked = true;
        _topSquareMouseDownPosition = e.GetPosition(this);
        e.Handled = true;
    }
    
    private void TopSquare_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_topSquareClicked)
        {
            // Vérifier si c'était un vrai clic (pas un drag)
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _topSquareMouseDownPosition;
            
            // Si le mouvement est minimal, c'est un clic et on toggle
            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            {
                // Toggle la fenêtre Kikimeter principale (ouvrir/fermer)
                OnOpenKikimeter?.Invoke(this, EventArgs.Empty);
            }
            
            _topSquareClicked = false;
            e.Handled = true;
            }
        }
    
    // Gestion du carré milieu (Loot)
    private bool _middleSquareClicked = false;
    private WpfPoint _middleSquareMouseDownPosition;
    
    private void MiddleSquare_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _middleSquareClicked = true;
        _middleSquareMouseDownPosition = e.GetPosition(this);
        e.Handled = true;
    }
    
    private void MiddleSquare_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_middleSquareClicked)
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _middleSquareMouseDownPosition;
        
            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            {
                OnOpenLoot?.Invoke(this, EventArgs.Empty);
            }
            _middleSquareClicked = false;
            e.Handled = true;
        }
    }
    
    // Gestion du carré Web
    private bool _webSquareClicked = false;
    private WpfPoint _webSquareMouseDownPosition;
    
    private void WebSquare_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _webSquareClicked = true;
        _webSquareMouseDownPosition = e.GetPosition(this);
        e.Handled = true;
    }
    
    private void WebSquare_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_webSquareClicked)
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _webSquareMouseDownPosition;
            
            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            {
                // Toggle la fenêtre Web
                OnOpenWeb?.Invoke(this, EventArgs.Empty);
            }
            _webSquareClicked = false;
            e.Handled = true;
        }
    }
    
    // Gestion du carré inférieur (Paramètres)
    private bool _bottomSquareClicked = false;
    private WpfPoint _bottomSquareMouseDownPosition;
    
    private void BottomSquare_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _bottomSquareClicked = true;
        _bottomSquareMouseDownPosition = e.GetPosition(this);
        e.Handled = true;
    }

    private void BottomSquare_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_bottomSquareClicked)
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _bottomSquareMouseDownPosition;
            
            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            {
                // Ouvrir SettingsWindow
                OnOpenSettings?.Invoke(this, EventArgs.Empty);
    }
            _bottomSquareClicked = false;
            e.Handled = true;
        }
    }
    
    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Gérer le cas où l'image web_icon.png n'existe pas encore
        // L'utilisateur pourra l'ajouter lui-même
        if (WebIcon != null)
        {
            try
            {
                // Vérifier si l'image existe, sinon utiliser une icône par défaut temporaire
                var uri = new Uri("pack://application:,,,/GameOverlay.Windows;component/web_icon.png");
                // Si l'image n'existe pas, WebIcon restera invisible mais le carré sera cliquable
            }
            catch
            {
                // Image non trouvée - l'utilisateur devra l'ajouter
            }
        }
        
        // Appliquer le thème au menu contextuel s'il existe
        if (this.ContextMenu != null)
        {
            ThemeManager.ApplyContextMenuTheme(this.ContextMenu);
            // Forcer le thème à chaque ouverture pour éviter le cyan
            this.ContextMenu.Opened += (s, args) =>
            {
                if (s is System.Windows.Controls.ContextMenu menu)
                {
                    ThemeManager.ApplyContextMenuTheme(menu);
                }
            };
        }
    }
    
    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        // Annuler l'ouverture du menu WPF par défaut
        e.Handled = true;
        
        // Utiliser le menu Windows Forms du MainWindow
        var window = Window.GetWindow(this);
        if (window != null)
        {
            // Utiliser la réflexion pour appeler GetMainWindowContextMenu si disponible
            var getMenuMethod = window.GetType().GetMethod("GetMainWindowContextMenu");
            if (getMenuMethod != null)
            {
                var contextMenu = getMenuMethod.Invoke(window, null) as System.Windows.Forms.ContextMenuStrip;
                if (contextMenu != null)
                {
                    // Obtenir la position de la souris à l'écran
                    var mousePos = System.Windows.Forms.Control.MousePosition;
                    contextMenu.Show(mousePos.X, mousePos.Y);
                }
            }
        }
    }
    
    private void ForceBrownTheme(System.Windows.Controls.ContextMenu menu)
    {
        if (menu == null) return;
        
        // Couleur de survol brune
        var brownHover = new SolidColorBrush(Color.FromArgb(150, 110, 92, 42)); // #966E5C2A
        
        // Forcer dans TOUS les ResourceDictionary possibles
        menu.Resources[SystemColors.MenuHighlightBrushKey] = brownHover;
        menu.Resources[SystemColors.HighlightBrushKey] = brownHover;
        menu.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = brownHover;
        
        // Appliquer le thème via ThemeManager
        ThemeManager.ApplyContextMenuTheme(menu);
        
        // Forcer sur chaque MenuItem avec un style inline
        foreach (var item in menu.Items.OfType<System.Windows.Controls.MenuItem>())
        {
            ForceBrownThemeOnMenuItem(item);
        }
        
        // S'abonner à l'événement Opened pour forcer à chaque ouverture
        menu.Opened -= Menu_Opened_ForceBrown;
        menu.Opened += Menu_Opened_ForceBrown;
    }
    
    private void Menu_Opened_ForceBrown(object? sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.ContextMenu menu)
        {
            ForceBrownTheme(menu);
        }
    }
    
    private void ForceBrownThemeOnMenuItem(System.Windows.Controls.MenuItem item)
    {
        if (item == null) return;
        
        var brownHover = new SolidColorBrush(Color.FromArgb(150, 110, 92, 42));
        
        // Forcer dans les ressources
        item.Resources[SystemColors.MenuHighlightBrushKey] = brownHover;
        item.Resources[SystemColors.HighlightBrushKey] = brownHover;
        item.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = brownHover;
        
        // Créer un style inline qui surcharge TOUT
        var style = new Style(typeof(MenuItem));
        
        // ImageBrush pour le fond
        var imageBrush = new ImageBrush();
        imageBrush.ImageSource = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/EndTurnWidgetBackground.png"));
        imageBrush.Stretch = Stretch.Fill;
        imageBrush.Freeze();
        
        style.Setters.Add(new Setter(MenuItem.BackgroundProperty, imageBrush));
        style.Setters.Add(new Setter(MenuItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(110, 92, 42))));
        
        // Trigger IsHighlighted avec couleur brune FORCÉE
        var trigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        trigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, brownHover));
        style.Triggers.Add(trigger);
        
        // Appliquer le style
        item.Style = style;
        
        // Récursif pour les sous-menus
        foreach (var subItem in item.Items.OfType<System.Windows.Controls.MenuItem>())
        {
            ForceBrownThemeOnMenuItem(subItem);
        }
    }
    
    private void ApplyMenuItemThemeRecursive(System.Windows.Controls.MenuItem menuItem)
    {
        if (menuItem == null) return;
        
        // Forcer les SystemColors dans les ressources de chaque MenuItem
        var hoverBrush = new SolidColorBrush(Color.FromArgb(150, 110, 92, 42)); // #966E5C2A
        menuItem.Resources[SystemColors.MenuHighlightBrushKey] = hoverBrush;
        menuItem.Resources[SystemColors.HighlightBrushKey] = hoverBrush;
        menuItem.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = hoverBrush;
        
        // Appliquer récursivement aux sous-menus
        foreach (var subItem in menuItem.Items.OfType<System.Windows.Controls.MenuItem>())
        {
            ApplyMenuItemThemeRecursive(subItem);
        }
    }
}

