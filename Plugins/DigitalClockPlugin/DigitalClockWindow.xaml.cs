using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GameOverlay.Models;
using Newtonsoft.Json;

namespace DigitalClockPlugin
{
    public class DigitalClockWindow : Window
    {
        // Constantes pour masquer la fenêtre d'Alt+Tab
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private readonly DispatcherTimer _timer;
        private readonly IPluginContext _context;
        private readonly string _configPath;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _fontSize = 48;
        private TextBlock _timeDisplay;
        private const string WindowId = "ClockWindow";
        private bool _isPluginActive = false;
        
        /// <summary>
        /// Permet de définir si le plugin est actif pour empêcher la fermeture accidentelle
        /// </summary>
        public bool IsPluginActive
        {
            get => _isPluginActive;
            set => _isPluginActive = value;
        }

        public DigitalClockWindow(IPluginContext context, string configPath)
        {
            _context = context;
            _configPath = configPath;
            
            // Configuration de la fenêtre
            Title = "Horloge Digitale";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            
            // Taille par défaut de la fenêtre (assez grande pour le texte)
            Width = 200;
            Height = 80;
            
            // Ne pas utiliser WindowStartupLocation pour permettre le chargement depuis la config
            WindowStartupLocation = WindowStartupLocation.Manual;
            
            // Créer le contenu simple
            var grid = new Grid();
            
            // Couleur du texte : Blanc
            var textColor = Colors.White;
            var textBrush = new SolidColorBrush(textColor);
            
            // Texte principal
            _timeDisplay = new TextBlock
            {
                Foreground = textBrush,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "00:00:00",
                Cursor = Cursors.Hand,
                FontSize = _fontSize
            };
            
            grid.Children.Add(_timeDisplay);
            Content = grid;
            
            // Gestionnaires d'événements
            MouseLeftButtonDown += Window_MouseLeftButtonDown;
            MouseLeftButtonUp += Window_MouseLeftButtonUp;
            MouseMove += Window_MouseMove;
            MouseWheel += Window_MouseWheel;
            Loaded += Window_Loaded;
            Closing += Window_Closing;
            
            // Timer pour mettre à jour l'heure chaque seconde
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            // Charger la position de la fenêtre depuis le système générique
            LoadWindowPosition();
            
            // Charger la configuration AVANT d'afficher la fenêtre
            LoadConfiguration();
            
            // Si aucune position n'a été chargée, centrer l'écran
            if (Left == 0 && Top == 0)
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
            }
            
            // Forcer la couleur après le chargement de la config (au cas où elle serait écrasée)
            _timeDisplay.Foreground = new SolidColorBrush(Colors.White);
            
            // Mettre à jour l'heure immédiatement
            UpdateTime();
        }
        
        private void LoadWindowPosition()
        {
            try
            {
                var position = _context.LoadWindowPosition(WindowId);
                if (position != null)
                {
                    // Vérifier que la position est valide (dans les limites de l'écran)
                    var screenWidth = SystemParameters.PrimaryScreenWidth;
                    var screenHeight = SystemParameters.PrimaryScreenHeight;
                    
                    if (position.Left >= 0 && position.Top >= 0 && 
                        position.Left < screenWidth && position.Top < screenHeight)
                    {
                        Left = position.Left;
                        Top = position.Top;
                        if (position.Width.HasValue) Width = position.Width.Value;
                        if (position.Height.HasValue) Height = position.Height.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur chargement position fenêtre: {ex.Message}");
            }
        }
        
        private void SaveWindowPosition()
        {
            try
            {
                _context.SaveWindowPosition(WindowId, Left, Top, Width, Height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur sauvegarde position fenêtre: {ex.Message}");
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                // Créer le dossier si nécessaire
                var configDir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                if (File.Exists(_configPath))
                {
                    var configJson = File.ReadAllText(_configPath);
                    var config = JsonConvert.DeserializeObject<ClockConfig>(configJson);
                    
                    if (config != null)
                    {
                        // Ne plus charger la position depuis config.json, elle est gérée par le système générique
                        // Charger uniquement les paramètres spécifiques (fontSize, couleur)
                        
                        if (config.FontSize > 0)
                        {
                            _fontSize = config.FontSize;
                            ApplyFontSize(_fontSize);
                        }
                        
                        // La couleur est maintenant fixe (blanc)
                        // On ignore la couleur de la config et on force la couleur
                        _timeDisplay.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignorer les erreurs de chargement de config
                System.Diagnostics.Debug.WriteLine($"Erreur chargement config: {ex.Message}");
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                // Sauvegarder la position via le système générique
                SaveWindowPosition();
                
                // Créer le dossier si nécessaire
                var configDir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                // Ne plus sauvegarder la position dans config.json, elle est gérée par le système générique
                // Sauvegarder uniquement les paramètres spécifiques (fontSize, couleur)
                var config = new ClockConfig
                {
                    FontSize = _fontSize,
                    ForegroundColor = GetColorFromBrush(_timeDisplay.Foreground)
                };
                
                var configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, configJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur sauvegarde config: {ex.Message}");
            }
        }

        private ColorRGB? GetColorFromBrush(Brush brush)
        {
            if (brush is SolidColorBrush solidBrush)
            {
                return new ColorRGB
                {
                    R = solidBrush.Color.R,
                    G = solidBrush.Color.G,
                    B = solidBrush.Color.B
                };
            }
            return null;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            string timeText = DateTime.Now.ToString("HH:mm:ss");
            _timeDisplay.Text = timeText;
            // Forcer la couleur à chaque mise à jour pour être sûr qu'elle ne soit pas écrasée
            _timeDisplay.Foreground = new SolidColorBrush(Colors.White);
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Masquer la fenêtre d'Alt+Tab
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
            
            // Forcer la couleur au chargement de la fenêtre
            _timeDisplay.Foreground = new SolidColorBrush(Colors.White);
            
            // S'assurer que la fenêtre est visible
            Visibility = Visibility.Visible;
            Show();
            
            // S'assurer que la fenêtre a une taille valide
            if (Width <= 0) Width = 200;
            if (Height <= 0) Height = 80;
        }
        
        private void ApplyFontSize(double size)
        {
            _timeDisplay.FontSize = size;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            CaptureMouse();
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                SaveConfiguration();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = PointToScreen(e.GetPosition(this));
                var newLeft = currentPosition.X - _dragStartPoint.X;
                var newTop = currentPosition.Y - _dragStartPoint.Y;
                
                // Limiter aux bounds de l'écran
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                newLeft = Math.Max(0, Math.Min(newLeft, screenWidth - Width));
                newTop = Math.Max(0, Math.Min(newTop, screenHeight - Height));
                
                Left = newLeft;
                Top = newTop;
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Changer la taille avec la molette de la souris
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _fontSize += e.Delta > 0 ? 2 : -2;
                _fontSize = Math.Max(12, Math.Min(200, _fontSize)); // Limiter entre 12 et 200
                ApplyFontSize(_fontSize);
                SaveConfiguration();
            }
        }
        
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Si le plugin est toujours actif, empêcher la fermeture et cacher la fenêtre à la place
            if (_isPluginActive)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            
            // Si le plugin n'est plus actif, permettre la fermeture normale
            _timer?.Stop();
            SaveConfiguration();
        }
        
        protected override void OnClosed(EventArgs e)
        {
            if (!_isPluginActive)
            {
                _timer?.Stop();
                SaveConfiguration();
            }
            base.OnClosed(e);
        }
    }

    public class ClockConfig
    {
        // Position X et Y retirées - maintenant gérées par le système générique de plugins
        public double FontSize { get; set; } = 48;
        public ColorRGB? ForegroundColor { get; set; }
    }

    public class ColorRGB
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }
}

