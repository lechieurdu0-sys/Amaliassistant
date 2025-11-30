using System;
using WpfColor = System.Windows.Media.Color;

namespace GameOverlay.Themes;

public class AccentColorChangedEventArgs : EventArgs
{
    public WpfColor NewColor { get; }

    public AccentColorChangedEventArgs(WpfColor newColor)
    {
        NewColor = newColor;
    }
}
