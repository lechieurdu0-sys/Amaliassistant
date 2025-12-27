using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using DrawingColor = System.Drawing.Color;

namespace GameOverlay.Themes
{
    /// <summary>
    /// Gestionnaire centralisé des thèmes de l'application
    /// Permet de changer la couleur d'accent (brun/marron par défaut : #FF6E5C2A) partout dans l'application
    /// </summary>
    public static class ThemeManager
    {
        // Couleur d'accent par défaut (brun/marron : RGB(110, 92, 42) = #FF6E5C2A)
        private static WpfColor _accentColor = WpfColor.FromRgb(110, 92, 42);
        
        // Couleur de fond des bulles par défaut
        private static string _bubbleBackgroundColor = "#FF1A1A1A";
        
        // Événement déclenché quand la couleur d'accent change
        public static event EventHandler<AccentColorChangedEventArgs>? AccentColorChanged;
        
        // Événement déclenché quand la couleur de fond des bulles change
        public static event EventHandler<BubbleBackgroundColorChangedEventArgs>? BubbleBackgroundColorChanged;

        /// <summary>
        /// Obtient ou définit la couleur d'accent de l'application
        /// Couleur par défaut : brun/marron (#FF6E5C2A)
        /// </summary>
        public static WpfColor AccentColor
        {
            get => _accentColor;
            set
            {
                if (_accentColor != value)
                {
                    _accentColor = value;
                    AccentColorChanged?.Invoke(null, new AccentColorChangedEventArgs(value));
                }
            }
        }

        /// <summary>
        /// Obtient la couleur d'accent en format WPF (SolidColorBrush)
        /// </summary>
        public static WpfSolidColorBrush AccentBrush => new WpfSolidColorBrush(_accentColor);

        /// <summary>
        /// Obtient la couleur d'accent en format System.Drawing.Color
        /// </summary>
        public static DrawingColor AccentDrawingColor => 
            DrawingColor.FromArgb(_accentColor.R, _accentColor.G, _accentColor.B);

        /// <summary>
        /// Obtient la couleur d'accent en format hexadécimal (#AARRGGBB)
        /// </summary>
        public static string AccentHex => $"#FF{_accentColor.R:X2}{_accentColor.G:X2}{_accentColor.B:X2}";

        /// <summary>
        /// Obtient la couleur d'accent en format hexadécimal court (#RRGGBB)
        /// </summary>
        public static string AccentHexShort => $"#{_accentColor.R:X2}{_accentColor.G:X2}{_accentColor.B:X2}";

        /// <summary>
        /// Définit la couleur d'accent depuis un code hexadécimal (#RRGGBB ou #AARRGGBB)
        /// </summary>
        public static void SetAccentColorFromHex(string hexColor)
        {
            try
            {
                hexColor = hexColor.Trim();
                
                // Supprimer le # s'il est présent
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }

                // Gérer le format AARRGGBB ou RRGGBB
                if (hexColor.Length == 8)
                {
                    // AARRGGBB - ignorer l'alpha pour la couleur d'accent
                    hexColor = hexColor.Substring(2);
                }
                
                if (hexColor.Length == 6)
                {
                    byte r = Convert.ToByte(hexColor.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hexColor.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hexColor.Substring(4, 2), 16);
                    AccentColor = WpfColor.FromRgb(r, g, b);
                }
                else
                {
                    throw new ArgumentException("Le format hexadécimal doit être #RRGGBB ou #AARRGGBB");
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Format de couleur invalide : {hexColor}", ex);
            }
        }

        /// <summary>
        /// Définit la couleur d'accent depuis les composantes RGB
        /// </summary>
        public static void SetAccentColor(byte r, byte g, byte b)
        {
            AccentColor = WpfColor.FromRgb(r, g, b);
        }

        /// <summary>
        /// Obtient la couleur d'accent avec une opacité spécifiée (0.0 à 1.0)
        /// </summary>
        public static WpfSolidColorBrush GetAccentBrushWithOpacity(double opacity)
        {
            opacity = Math.Max(0.0, Math.Min(1.0, opacity));
            return new WpfSolidColorBrush(WpfColor.FromArgb((byte)(opacity * 255), _accentColor.R, _accentColor.G, _accentColor.B));
        }

        /// <summary>
        /// Obtient une couleur de survol basée sur la couleur d'accent (légèrement plus sombre)
        /// </summary>
        public static WpfColor HoverColor
        {
            get
            {
                // Couleur de survol : accent + un peu plus sombre
                return WpfColor.FromRgb(
                    (byte)Math.Max(0, _accentColor.R - 30),
                    (byte)Math.Max(0, _accentColor.G - 30),
                    (byte)Math.Max(0, _accentColor.B - 30)
                );
            }
        }

        /// <summary>
        /// Obtient le brush de survol
        /// </summary>
        public static WpfSolidColorBrush HoverBrush => new WpfSolidColorBrush(HoverColor);

        /// <summary>
        /// Réinitialise la couleur d'accent au brun/marron par défaut (#FF6E5C2A)
        /// </summary>
        public static void ResetToDefault()
        {
            AccentColor = WpfColor.FromRgb(110, 92, 42);
        }
        
        /// <summary>
        /// Obtient ou définit la couleur de fond des bulles (en hexadécimal, ex: "#FF1A1A1A")
        /// </summary>
        public static string BubbleBackgroundColor
        {
            get => _bubbleBackgroundColor;
            set
            {
                if (_bubbleBackgroundColor != value)
                {
                    _bubbleBackgroundColor = value;
                    BubbleBackgroundColorChanged?.Invoke(null, new BubbleBackgroundColorChangedEventArgs(value));
                }
            }
        }
        
        /// <summary>
        /// Obtient la couleur de fond des bulles sous forme de SolidColorBrush
        /// </summary>
        public static WpfSolidColorBrush BubbleBackgroundBrush
        {
            get
            {
                try
                {
                    var color = (WpfColor)ColorConverter.ConvertFromString(_bubbleBackgroundColor);
                    return new WpfSolidColorBrush(color);
                }
                catch
                {
                    return new WpfSolidColorBrush(WpfColor.FromRgb(26, 26, 26));
                }
            }
        }
        
        /// <summary>
        /// Réinitialise la couleur de fond des bulles au fond sombre par défaut
        /// </summary>
        public static void ResetBubbleBackgroundToDefault()
        {
            BubbleBackgroundColor = "#FF1A1A1A";
        }
        
        /// <summary>
        /// Applique la couleur d'accent et la couleur de fond des bulles à un menu contextuel WPF.
        /// </summary>
        public static void ApplyContextMenuTheme(ContextMenu contextMenu)
        {
            if (contextMenu == null)
            {
                return;
            }
            
            try
            {
                // Utiliser l'image de fond comme dans PluginManagerWindow
                var imageBrush = new ImageBrush();
                imageBrush.ImageSource = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/EndTurnWidgetBackground.png"));
                imageBrush.Stretch = Stretch.Fill;
                imageBrush.Freeze();
                
                // Contour : #FF6E5C2A (RGB: 110, 92, 42)
                var borderColor = Color.FromRgb(110, 92, 42);
                // Texte plus clair pour être visible : #FF6E5C2A (marron moyen) ou plus clair encore
                var textColor = Color.FromRgb(110, 92, 42); // #FF6E5C2A - marron moyen plus visible
                
                var accentBrushClone = new SolidColorBrush(borderColor);
                var foregroundBrushClone = new SolidColorBrush(textColor);
                
                contextMenu.Background = imageBrush;
                contextMenu.BorderBrush = accentBrushClone;
                contextMenu.BorderThickness = new Thickness(1);
                contextMenu.Foreground = foregroundBrushClone;
                
                // Utiliser la couleur secondaire pour le texte en surbrillance
                var highlightTextBrush = foregroundBrushClone;
                
                ApplySystemColorOverrides(contextMenu.Resources, imageBrush, accentBrushClone, highlightTextBrush);
                
                ApplyMenuItemsTheme(contextMenu.Items.OfType<MenuItem>(), imageBrush, accentBrushClone, highlightTextBrush);
                
                // S'assurer que la couleur de survol brune est bien appliquée via les SystemColors
                // Forcer l'override pour éviter que le cyan ne s'affiche
                var hoverBrush = new SolidColorBrush(Color.FromArgb(150, 110, 92, 42)); // #966E5C2A - brun semi-transparent
                contextMenu.Resources[SystemColors.MenuHighlightBrushKey] = hoverBrush;
                contextMenu.Resources[SystemColors.HighlightBrushKey] = hoverBrush;
                contextMenu.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = hoverBrush;
                
                // Rendre tous les séparateurs invisibles
                foreach (var separator in contextMenu.Items.OfType<Separator>())
                {
                    separator.Height = 8;
                    separator.Background = Brushes.Transparent;
                    separator.BorderBrush = Brushes.Transparent;
                    separator.Opacity = 0;
                    separator.Margin = new Thickness(0, 2, 0, 2);
                }
            }
            catch
            {
                // Ignorer silencieusement pour éviter de perturber l'utilisateur si un menu n'est pas stylable
            }
        }
        
        private static void ApplySystemColorOverrides(ResourceDictionary resources, Brush backgroundBrush, Brush accentBrush, Brush highlightTextBrush)
        {
            resources[SystemColors.ControlBrushKey] = backgroundBrush;
            resources[SystemColors.MenuBrushKey] = backgroundBrush;
            resources[SystemColors.MenuBarBrushKey] = backgroundBrush;
            // Couleur de survol brune : #FF6E5C2A avec alpha 150 (RGB: 110, 92, 42 avec alpha 150)
            var hoverColor = Color.FromArgb(150, 110, 92, 42); // #966E5C2A - brun semi-transparent
            resources[SystemColors.InactiveSelectionHighlightBrushKey] = new SolidColorBrush(hoverColor);
            resources[SystemColors.MenuHighlightBrushKey] = new SolidColorBrush(hoverColor);
            resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(hoverColor);
            resources[SystemColors.HighlightTextBrushKey] = highlightTextBrush;
            resources[SystemColors.ControlTextBrushKey] = accentBrush;
        }
        
        private static void ApplyMenuItemsTheme(IEnumerable<MenuItem> menuItems, Brush backgroundBrush, Brush accentBrush, Brush highlightTextBrush)
        {
            // Couleur de survol brune : #FF6E5C2A avec alpha 150 (RGB: 110, 92, 42 avec alpha 150)
            var hoverColor = Color.FromArgb(150, 110, 92, 42); // #966E5C2A - brun semi-transparent
            var hoverBrush = new SolidColorBrush(hoverColor);
            
            // Image de fond pour les items (réutiliser celle passée en paramètre si c'est une ImageBrush)
            Brush itemBackgroundBrush = backgroundBrush;
            if (!(backgroundBrush is ImageBrush))
            {
                // Créer une nouvelle ImageBrush si ce n'est pas déjà une ImageBrush
                var imageBrush = new ImageBrush();
                imageBrush.ImageSource = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/EndTurnWidgetBackground.png"));
                imageBrush.Stretch = Stretch.Fill;
                imageBrush.Freeze();
                itemBackgroundBrush = imageBrush;
            }
            
            foreach (var menuItem in menuItems)
            {
                menuItem.Background = itemBackgroundBrush;
                menuItem.BorderBrush = accentBrush;
                menuItem.Foreground = highlightTextBrush;
                
                // Forcer la couleur de survol via le style
                var style = new Style(typeof(MenuItem));
                style.Setters.Add(new Setter(MenuItem.BackgroundProperty, itemBackgroundBrush));
                style.Setters.Add(new Setter(MenuItem.ForegroundProperty, highlightTextBrush));
                style.Setters.Add(new Setter(MenuItem.BorderBrushProperty, accentBrush));
                
                // Trigger pour le survol
                var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, hoverBrush));
                style.Triggers.Add(hoverTrigger);
                
                // Trigger pour la sélection
                var selectedTrigger = new Trigger { Property = MenuItem.IsSubmenuOpenProperty, Value = true };
                selectedTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, hoverBrush));
                style.Triggers.Add(selectedTrigger);
                
                menuItem.Style = style;
                
                ApplySystemColorOverrides(menuItem.Resources, itemBackgroundBrush, accentBrush, highlightTextBrush);
                
                if (menuItem.Items.Count > 0)
                {
                    ApplyMenuItemsTheme(menuItem.Items.OfType<MenuItem>(), itemBackgroundBrush, accentBrush, highlightTextBrush);
                }
            }
        }
    }
    
    /// <summary>
    /// Arguments de l'événement déclenché quand la couleur de fond des bulles change
    /// </summary>
    public class BubbleBackgroundColorChangedEventArgs : EventArgs
    {
        public string NewColor { get; }
        
        public BubbleBackgroundColorChangedEventArgs(string newColor)
        {
            NewColor = newColor;
        }
    }
}



