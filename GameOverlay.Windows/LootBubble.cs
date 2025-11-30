using System;
using System.Windows;
using System.Windows.Controls;
using GameOverlay.Models;

namespace GameOverlay.Windows;

/// <summary>
/// Classe LootBubble - non utilisée, intégrée dans KikimeterBubble
/// Classe minimale pour éviter les erreurs de compilation
/// </summary>
public partial class LootBubble : UserControl
{
    public LootBubble(string chatLogPath, Config config, int x, int y, double opacity, double size)
    {
        // Classe non utilisée - intégrée dans KikimeterBubble
    }
    
    public event EventHandler? OnOpenLoot;
    public event EventHandler? OnOpenPlayerOrder;
    public event EventHandler<System.Windows.Point>? PositionChanged;
    public event EventHandler<double>? SizeChanged;
    public event EventHandler<double>? OpacityChanged;
    
    public void UpdateBackgroundWithOpacity(double opacity, string colorHex)
    {
        // Classe non utilisée
    }
}



