using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GameOverlay.Models;
using GameOverlay.Themes;
using Newtonsoft.Json;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace GameOverlay.Kikimeter.Views
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
        private bool _isDragging = false;
        private Point _dragStartPoint;

        private CustomMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon, Window? owner = null)
        {
            InitializeComponent();
            
            TitleTextBlock.Text = title ?? "Message";
            MessageTextBlock.Text = message ?? "";
            
            // Si le owner est SettingsWindow, utiliser le même background que SettingsWindow
            if (owner is SettingsWindow)
            {
                // Vérifier si SettingsWindow a une couleur de fond personnalisée configurée
                try
                {
                    var configPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Amaliassistant",
                        "config.json");
                    
                    if (System.IO.File.Exists(configPath))
                    {
                        var json = System.IO.File.ReadAllText(configPath);
                        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<GameOverlay.Models.Config>(json);
                        
                        if (config != null && config.KikimeterWindowBackgroundEnabled && 
                            !string.IsNullOrEmpty(config.KikimeterWindowBackgroundColor))
                        {
                            // Utiliser la couleur de fond personnalisée
                            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                                config.KikimeterWindowBackgroundColor);
                            color.A = (byte)(config.KikimeterWindowBackgroundOpacity * 255);
                            var colorBrush = new SolidColorBrush(color);
                            colorBrush.Freeze();
                            
                            Resources["WindowBackgroundBrush"] = colorBrush;
                            Background = colorBrush;
                            // Appliquer aussi au Grid principal
                            if (FindName("MainGrid") is System.Windows.Controls.Grid mainGrid)
                            {
                                mainGrid.Background = colorBrush;
                            }
                        }
                        else
                        {
                            // Utiliser l'image loot.png comme background, comme dans SettingsWindow
                            SetLootBackground();
                        }
                    }
                    else
                    {
                        // Pas de fichier de config, utiliser l'image messagebox.png
                        SetLootBackground();
                    }
                }
                catch
                {
                    // En cas d'erreur, utiliser l'image messagebox.png
                    SetLootBackground();
                }
            }
            
            // Configurer les boutons selon le type demandé
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    OkButton.Visibility = Visibility.Visible;
                    YesButton.Visibility = Visibility.Collapsed;
                    NoButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.OKCancel:
                    OkButton.Content = "OK";
                    OkButton.Visibility = Visibility.Visible;
                    NoButton.Content = "Annuler";
                    NoButton.Visibility = Visibility.Visible;
                    YesButton.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNo:
                    OkButton.Visibility = Visibility.Collapsed;
                    YesButton.Content = "Oui";
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Content = "Non";
                    NoButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    YesButton.Content = "Oui";
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Content = "Non";
                    NoButton.Visibility = Visibility.Visible;
                    OkButton.Content = "Annuler";
                    OkButton.Visibility = Visibility.Visible;
                    break;
            }
            
            // Initialiser le thème
            var accent = ThemeManager.AccentBrush;
            ApplyTheme(accent);
            ThemeManager.AccentColorChanged += ThemeManager_AccentColorChanged;
        }
        
        private void SetLootBackground()
        {
            var imageBrush = new ImageBrush();
            var bitmapImage = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/GameOverlay.Kikimeter;component/Views/messagebox.png"));
            imageBrush.ImageSource = bitmapImage;
            imageBrush.Stretch = Stretch.Fill;
            imageBrush.Freeze();
            
            Resources["WindowBackgroundBrush"] = imageBrush;
            Background = imageBrush;
            // Appliquer aussi au Grid principal
            if (FindName("MainGrid") is System.Windows.Controls.Grid mainGrid)
            {
                mainGrid.Background = imageBrush;
            }
        }
        
        private void SetSettingsStyleBackground()
        {
            // Utiliser une couleur jaune-marron qui correspond au style de SettingsWindow
            // Couleur proche de celle de loot.png : jaune mélangé avec du marron
            // RGB(208, 182, 148) - jaune-marron légèrement plus foncé et marron
            var color = System.Windows.Media.Color.FromRgb(0xD0, 0xB6, 0x94); // Jaune-marron plus foncé
            var colorBrush = new SolidColorBrush(color);
            colorBrush.Freeze();
            
            Resources["WindowBackgroundBrush"] = colorBrush;
            Background = colorBrush;
            // Appliquer aussi au Grid principal
            if (FindName("MainGrid") is System.Windows.Controls.Grid mainGrid)
            {
                mainGrid.Background = colorBrush;
            }
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

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            // Si c'est un bouton "Annuler" dans le cas OKCancel
            if (NoButton.Content.ToString() == "Annuler")
            {
                Result = MessageBoxResult.Cancel;
            }
            else
            {
                Result = MessageBoxResult.No;
            }
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
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

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TitleBar_MouseLeftButtonDown(sender, e);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            ThemeManager.AccentColorChanged -= ThemeManager_AccentColorChanged;
        }

        // Méthode statique pour afficher la MessageBox personnalisée
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            Window? owner = System.Windows.Application.Current.MainWindow;
            
            // Détecter si la fenêtre active est SettingsWindow
            var activeWindow = System.Windows.Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive);
            if (activeWindow is SettingsWindow settingsWindow)
            {
                owner = settingsWindow;
            }
            
            var dialog = new CustomMessageBox(messageBoxText, caption, button, icon, owner);
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            dialog.ShowDialog();
            return dialog.Result;
        }

        // Surcharges pour compatibilité
        public static MessageBoxResult Show(string messageBoxText)
        {
            return Show(messageBoxText, "Message", MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            var dialog = new CustomMessageBox(messageBoxText, caption, button, icon, owner);
            dialog.Owner = owner;
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}

