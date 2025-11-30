using System;
using System.Windows;
using GameOverlay.Models;

namespace GameOverlay.Kikimeter;

public partial class PlayerWindow : Window
{
    public string PlayerName { get; private set; } = "En attente...";
    public int KikiCount { get; private set; } = 0;
    private KikimeterWindow? _parentWindow;

    public PlayerWindow(string playerName, KikimeterWindow? parentWindow = null)
    {
        InitializeComponent();
        PlayerName = playerName;
        _parentWindow = parentWindow;
        
        if (PlayerNameText != null)
            PlayerNameText.Text = playerName;
        
        // Afficher le bouton retour si on a un parent
        if (ReturnButton != null && _parentWindow != null)
        {
            ReturnButton.Visibility = Visibility.Visible;
        }
        
        UpdateKikiCount(0);
        LoadWindowPosition();
        
        LocationChanged += (s, e) => SaveWindowPosition();
        Closing += (s, e) => SaveWindowPosition();
        
        Logger.Info("PlayerWindow", $"PlayerWindow créé pour {playerName}");
    }

    private void ReturnButton_Click(object sender, RoutedEventArgs e)
    {
        Logger.Debug("PlayerWindow", $"Retour à la fenêtre principale demandé pour {PlayerName}");
        if (_parentWindow != null)
        {
            // Toggle pour revenir au mode normal
            _parentWindow.ToggleToNormalMode();
        }
    }

    public void UpdateKikiCount(int count)
    {
        KikiCount = count;
        if (KikiCountText != null)
            KikiCountText.Text = $"Kikis: {count}";
    }

    private void LoadWindowPosition()
    {
        try
        {
            var positions = PersistentStorageHelper.LoadJsonWithFallback<PlayerWindowPositions>("player_window_positions.json");
            
            if (positions != null && positions.ContainsKey(PlayerName))
            {
                var pos = positions[PlayerName];
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
            
            positions[PlayerName] = new WindowPosition
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

