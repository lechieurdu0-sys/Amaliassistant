using System;
using System.Windows;
using System.Windows.Input;
using GameOverlay.Models;
using FormsColorDialog = System.Windows.Forms.ColorDialog;

namespace GameOverlay.Windows;

public partial class WebWindowStyleDialog : Window
{
    private Config? _config;
    private bool _initializing = true;

    public WebWindowStyleDialog(Config? config)
    {
        InitializeComponent();
        _config = config;
        LoadSettings();
        _initializing = false;
    }

    private void LoadSettings()
    {
        if (_config == null) return;

        try
        {
            // Charger l'opacité de la fenêtre
            WindowOpacitySlider.Value = Math.Max(0.1, Math.Min(1.0, _config.WebWindowOpacity));
            UpdateWindowOpacityText(WindowOpacitySlider.Value);

            // Charger l'opacité du contenu
            ContentOpacitySlider.Value = Math.Max(0.1, Math.Min(1.0, _config.WebView2Opacity));
            UpdateContentOpacityText(ContentOpacitySlider.Value);

            // Charger le style
            BackgroundEnabledCheckBox.IsChecked = _config.WebWindowBackgroundEnabled;
            BackgroundColorTextBox.Text = _config.WebWindowBackgroundColor ?? "#FF1A1A1A";
            BackgroundOpacitySlider.Value = Math.Max(0.0, Math.Min(1.0, _config.WebWindowBackgroundOpacity));
            UpdateBackgroundOpacityText(BackgroundOpacitySlider.Value);
            TitleBarColorTextBox.Text = _config.WebWindowTitleBarColor ?? "#FF2A2A2A";
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindowStyleDialog", $"Erreur lors du chargement des paramètres: {ex.Message}");
        }
    }

    private void WindowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing) return;
        UpdateWindowOpacityText(e.NewValue);
        if (_config != null)
        {
            _config.WebWindowOpacity = e.NewValue;
        }
    }

    private void UpdateWindowOpacityText(double value)
    {
        WindowOpacityText.Text = $"{(int)(value * 100)}%";
    }

    private void ContentOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing) return;
        UpdateContentOpacityText(e.NewValue);
        if (_config != null)
        {
            _config.WebView2Opacity = e.NewValue;
        }
    }

    private void UpdateContentOpacityText(double value)
    {
        ContentOpacityText.Text = $"{(int)(value * 100)}%";
    }

    private void BackgroundOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing) return;
        UpdateBackgroundOpacityText(e.NewValue);
        if (_config != null)
        {
            _config.WebWindowBackgroundOpacity = e.NewValue;
        }
    }

    private void UpdateBackgroundOpacityText(double value)
    {
        BackgroundOpacityText.Text = $"{(int)(value * 100)}%";
    }

    private void BackgroundEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        if (_config != null)
        {
            _config.WebWindowBackgroundEnabled = BackgroundEnabledCheckBox.IsChecked ?? true;
        }
    }

    private void BackgroundColorTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyBackgroundColor();
        }
    }

    private void TitleBarColorTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyTitleBarColor();
        }
    }

    private void ChooseBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using (var colorDialog = new FormsColorDialog())
            {
                string currentColorHex = BackgroundColorTextBox.Text;
                try
                {
                    var currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColorHex);
                    colorDialog.Color = System.Drawing.Color.FromArgb(
                        currentColor.A,
                        currentColor.R,
                        currentColor.G,
                        currentColor.B);
                }
                catch
                {
                    // Utiliser une couleur par défaut si le parsing échoue
                }

                colorDialog.FullOpen = true;
                colorDialog.AllowFullOpen = true;

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedColor = colorDialog.Color;
                    string hexColor = $"#{selectedColor.A:X2}{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
                    BackgroundColorTextBox.Text = hexColor;
                    ApplyBackgroundColor();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindowStyleDialog", $"Erreur lors de la sélection de couleur: {ex.Message}");
            MessageBox.Show($"Erreur lors de la sélection de couleur: {ex.Message}",
                          "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChooseTitleBarColor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using (var colorDialog = new FormsColorDialog())
            {
                string currentColorHex = TitleBarColorTextBox.Text;
                try
                {
                    var currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColorHex);
                    colorDialog.Color = System.Drawing.Color.FromArgb(
                        currentColor.A,
                        currentColor.R,
                        currentColor.G,
                        currentColor.B);
                }
                catch
                {
                    // Utiliser une couleur par défaut si le parsing échoue
                }

                colorDialog.FullOpen = true;
                colorDialog.AllowFullOpen = true;

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedColor = colorDialog.Color;
                    string hexColor = $"#{selectedColor.A:X2}{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
                    TitleBarColorTextBox.Text = hexColor;
                    ApplyTitleBarColor();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindowStyleDialog", $"Erreur lors de la sélection de couleur: {ex.Message}");
            MessageBox.Show($"Erreur lors de la sélection de couleur: {ex.Message}",
                          "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyBackgroundColor()
    {
        if (_config != null)
        {
            try
            {
                // Valider la couleur
                var testColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(BackgroundColorTextBox.Text);
                _config.WebWindowBackgroundColor = BackgroundColorTextBox.Text;
            }
            catch
            {
                MessageBox.Show("Format de couleur invalide. Utilisez le format #AARRGGBB (ex: #FF1A1A1A)",
                              "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ApplyTitleBarColor()
    {
        if (_config != null)
        {
            try
            {
                // Valider la couleur
                var testColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(TitleBarColorTextBox.Text);
                _config.WebWindowTitleBarColor = TitleBarColorTextBox.Text;
            }
            catch
            {
                MessageBox.Show("Format de couleur invalide. Utilisez le format #AARRGGBB (ex: #FF2A2A2A)",
                              "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Appliquer toutes les modifications
        ApplyBackgroundColor();
        ApplyTitleBarColor();
        this.DialogResult = true;
        this.Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }
}



