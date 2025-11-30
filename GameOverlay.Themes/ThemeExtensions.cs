using WpfColor = System.Windows.Media.Color;
using DrawingColor = System.Drawing.Color;

namespace GameOverlay.Themes
{
    /// <summary>
    /// Extensions pour faciliter l'utilisation du ThemeManager
    /// </summary>
    public static class ThemeExtensions
    {
        /// <summary>
        /// Convertit une System.Windows.Media.Color en System.Drawing.Color
        /// </summary>
        public static DrawingColor ToDrawingColor(this WpfColor wpfColor)
        {
            return DrawingColor.FromArgb(wpfColor.R, wpfColor.G, wpfColor.B);
        }

        /// <summary>
        /// Convertit une System.Drawing.Color en System.Windows.Media.Color
        /// </summary>
        public static WpfColor ToWpfColor(this DrawingColor drawingColor)
        {
            return WpfColor.FromRgb(drawingColor.R, drawingColor.G, drawingColor.B);
        }
    }
}

