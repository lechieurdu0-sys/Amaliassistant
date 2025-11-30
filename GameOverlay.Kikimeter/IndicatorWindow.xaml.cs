using System;
using System.Windows;
using GameOverlay.Models;

namespace GameOverlay.Kikimeter;

public partial class IndicatorWindow : Window
{
    public IndicatorWindow()
    {
        InitializeComponent();
        LoadWindowPosition();
        LocationChanged += (s, e) => SaveWindowPosition();
        Closing += (s, e) => SaveWindowPosition();
        Logger.Info("KikimeterWindow", "Fenêtre d'indication vide créée pour le mode individuel");
    }

    private void LoadWindowPosition()
    {
        try
        {
            var positions = PersistentStorageHelper.LoadJsonWithFallback<PlayerWindowPositions>("player_window_positions.json");
            
            if (positions != null && positions.ContainsKey("INDICATOR_EMPTY"))
            {
                var pos = positions["INDICATOR_EMPTY"];
                Left = pos.Left;
                Top = pos.Top;
                Width = pos.Width;
                Height = pos.Height;
            }
        }
        catch { }
    }

    private void SaveWindowPosition()
    {
        try
        {
            var positions = PersistentStorageHelper.LoadJsonWithFallback<PlayerWindowPositions>("player_window_positions.json");
            
            positions["INDICATOR_EMPTY"] = new WindowPosition
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            };
            
            PersistentStorageHelper.SaveJson("player_window_positions.json", positions);
        }
        catch { }
    }
}






