using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Point = System.Windows.Point;
using Microsoft.Win32;
using CustomMessageBox = GameOverlay.Kikimeter.Views.CustomMessageBox;

namespace GameOverlay.App
{
    public partial class LogPathConfigDialog : Window
    {
        public string LogPath { get; private set; } = "";
        private readonly string _fileFilter;
        private readonly string _dialogTitle;
        
        public new string Title { get; set; }

        public LogPathConfigDialog(string title, string fileFilter, string currentPath = "")
        {
            InitializeComponent();
            _fileFilter = fileFilter;
            _dialogTitle = title;
            Title = title;
            DataContext = this;
            
            if (!string.IsNullOrEmpty(currentPath))
            {
                PathTextBox.Text = currentPath;
            }
            
            Loaded += LogPathConfigDialog_Loaded;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            try
            {
                // Couleur d'accent depuis ThemeManager
                var accentBrush = GameOverlay.Themes.ThemeManager.AccentBrush;
                var textBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
                
                // Appliquer au Grid principal (fond sombre)
                MainGrid.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CC000000"));
                
                // Appliquer les couleurs aux contrôles
                ApplyThemeToControls(MainGrid, accentBrush, textBrush);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur application thème: {ex.Message}");
            }
        }

        private void ApplyThemeToControls(System.Windows.DependencyObject parent, System.Windows.Media.SolidColorBrush accentBrush, System.Windows.Media.SolidColorBrush textBrush)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is System.Windows.Controls.TextBlock textBlock)
                {
                    textBlock.Foreground = textBrush;
                }
                else if (child is System.Windows.Controls.TextBox textBox)
                {
                    textBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 40, 40, 40));
                    textBox.Foreground = textBrush;
                    textBox.BorderBrush = accentBrush;
                    textBox.BorderThickness = new Thickness(1);
                    textBox.CaretBrush = accentBrush;
                }
                else if (child is System.Windows.Controls.Button button && button != CancelButton && button != OkButton)
                {
                    button.Background = accentBrush;
                    button.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0));
                    button.BorderBrush = accentBrush;
                }
                
                // Parcourir récursivement
                ApplyThemeToControls(child, accentBrush, textBrush);
            }
        }

        private void LogPathConfigDialog_Loaded(object sender, RoutedEventArgs e)
        {
            PathTextBox.Focus();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = _fileFilter,
                Title = _dialogTitle,
                CheckFileExists = true
            };

            // Si un chemin est déjà saisi, utiliser son répertoire comme point de départ
            if (!string.IsNullOrEmpty(PathTextBox.Text) && File.Exists(PathTextBox.Text))
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(PathTextBox.Text);
                openFileDialog.FileName = Path.GetFileName(PathTextBox.Text);
            }

            if (openFileDialog.ShowDialog() == true)
            {
                PathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string path = PathTextBox.Text.Trim();
            
            // Si le chemin n'est pas vide, vérifier qu'il existe
            if (!string.IsNullOrEmpty(path))
            {
                if (!File.Exists(path))
                {
                    CustomMessageBox.Show(
                        "Le fichier spécifié n'existe pas. Veuillez vérifier le chemin.",
                        "Fichier introuvable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            
            LogPath = path;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !(e.OriginalSource is System.Windows.Controls.Button))
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(this);
                CaptureMouse();
            }
        }

        private bool _isDragging = false;
        private Point _dragStartPoint;

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
    }
}
