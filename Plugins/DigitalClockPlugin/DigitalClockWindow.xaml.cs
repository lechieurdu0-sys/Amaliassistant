using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace DigitalClockPlugin
{
    public class DigitalClockWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly string _configPath;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _fontSize = 48;
        private TextBlock _timeDisplay;

        public DigitalClockWindow(string configPath)
        {
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
            
            // Timer pour mettre à jour l'heure chaque seconde
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
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
                        // Utiliser les coordonnées sauvegardées si elles sont valides
                        // Vérifier que la position est dans les limites de l'écran
                        var screenWidth = SystemParameters.PrimaryScreenWidth;
                        var screenHeight = SystemParameters.PrimaryScreenHeight;
                        
                        if (config.X >= 0 && config.Y >= 0 && 
                            config.X < screenWidth && config.Y < screenHeight)
                        {
                            Left = config.X;
                            Top = config.Y;
                        }
                        
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
                // Créer le dossier si nécessaire
                var configDir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                var config = new ClockConfig
                {
                    X = Left,
                    Y = Top,
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
        
        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            SaveConfiguration();
            base.OnClosed(e);
        }
    }

    public class ClockConfig
    {
        public double X { get; set; }
        public double Y { get; set; }
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

