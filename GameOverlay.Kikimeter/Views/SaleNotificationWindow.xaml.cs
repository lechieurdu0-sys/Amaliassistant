using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;
using GameOverlay.Kikimeter.Services;

namespace GameOverlay.Kikimeter.Views;

/// <summary>
/// Fenêtre de notification pour afficher les informations de vente
/// </summary>
public partial class SaleNotificationWindow : Window
{
    private static readonly DropShadowEffect DropShadowEffect = new DropShadowEffect
    {
        Color = System.Windows.Media.Colors.Black,
        Direction = 315,
        ShadowDepth = 5,
        Opacity = 0.5,
        BlurRadius = 10
    };
    
    private bool _isDragging = false;
    private System.Drawing.Point _dragStartScreenPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private bool _hasMoved = false;
    
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out System.Drawing.Point lpPoint);
    private System.Windows.Threading.DispatcherTimer? _autoCloseTimer;
    private bool _timerStarted = false;
    
    /// <summary>
    /// Démarre le timer de fermeture automatique (seulement si la notification est visible)
    /// </summary>
    public void StartAutoCloseTimer()
    {
        if (_timerStarted || _autoCloseTimer != null)
            return;
            
        _timerStarted = true;
        _autoCloseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;
            Close();
        };
        _autoCloseTimer.Start();
    }
    
    /// <summary>
    /// Arrête le timer de fermeture automatique
    /// </summary>
    public void StopAutoCloseTimer()
    {
        if (_autoCloseTimer != null)
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer = null;
        }
        _timerStarted = false;
    }
    
    /// <param name="saleInfo">Informations de vente</param>
    /// <param name="showAbsenceMessage">Si true, ajoute "pendant votre absence" au message</param>
    public SaleNotificationWindow(SaleInfo saleInfo, bool showAbsenceMessage = false)
    {
        InitializeComponent();
        
        // Appliquer l'effet d'ombre
        if (Content is System.Windows.Controls.Border border)
        {
            border.Effect = DropShadowEffect;
        }
        
        // Construire le message avec l'image de kamas au lieu du "k"
        string baseMessage = $"Vous avez vendu {saleInfo.ItemCount} objet{(saleInfo.ItemCount > 1 ? "s" : "")} pour un prix total de {saleInfo.TotalKamas.ToString("N0", CultureInfo.InvariantCulture)} ";
        
        MessageTextRun.Text = baseMessage;
        
        if (showAbsenceMessage)
        {
            AbsenceTextRun.Text = " pendant votre absence";
        }
        else
        {
            AbsenceTextRun.Text = "";
        }
        
        // Charger la position sauvegardée
        LoadSavedPosition();
        
        // Le timer sera démarré seulement quand la notification devient visible (au-dessus)
        // Voir MainWindow.ReorganizeSaleNotificationsZOrder()
    }
    
    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Libérer la capture de souris seulement si on est en train de drag
        if (this.IsMouseCaptured && _isDragging)
        {
            this.ReleaseMouseCapture();
            _isDragging = false;
        }
        // Ne pas gérer l'événement pour permettre la propagation au menu contextuel de la fenêtre principale
        e.Handled = false;
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Libérer la capture de souris si elle est active
        if (this.IsMouseCaptured)
        {
            this.ReleaseMouseCapture();
        }
        StopAutoCloseTimer();
        base.OnClosed(e);
    }
    
    protected override void OnLostMouseCapture(System.Windows.Input.MouseEventArgs e)
    {
        // Si on perd la capture de souris pendant un drag, sauvegarder la position
        if (_isDragging && _hasMoved)
        {
            SavePosition();
        }
        // Ne pas réinitialiser _isDragging ici car MouseLeftButtonUp le fera
        base.OnLostMouseCapture(e);
    }
    
    private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Ne capturer que si c'est un clic gauche
        if (e.ChangedButton != MouseButton.Left)
        {
            // Libérer la capture si c'est un autre bouton (ex: clic droit pour le menu)
            if (this.IsMouseCaptured)
            {
                this.ReleaseMouseCapture();
                _isDragging = false;
            }
            return;
        }
        
        _isDragging = true;
        _hasMoved = false;
        
        // Obtenir la position de la souris directement depuis l'API Windows
        GetCursorPos(out _dragStartScreenPoint);
        
        _dragStartLeft = Left;
        _dragStartTop = Top;
        
        // Capturer la souris au niveau de la fenêtre pour continuer à recevoir les événements même si la souris sort
        this.CaptureMouse();
        e.Handled = true;
    }
    
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        
        // Obtenir la position actuelle de la souris directement depuis l'API Windows
        GetCursorPos(out System.Drawing.Point currentScreenPoint);
        
        // Calculer le delta depuis la position initiale
        var deltaX = currentScreenPoint.X - _dragStartScreenPoint.X;
        var deltaY = currentScreenPoint.Y - _dragStartScreenPoint.Y;
        
        // Si le mouvement est significatif (plus de 1 pixel), considérer qu'on déplace
        if (Math.Abs(deltaX) > 1 || Math.Abs(deltaY) > 1)
        {
            _hasMoved = true;
            // Appliquer le delta à la position initiale de la fenêtre
            Left = _dragStartLeft + deltaX;
            Top = _dragStartTop + deltaY;
        }
    }
    
    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        
        if (!_isDragging)
        {
            return;
        }
        
        _isDragging = false;
        this.ReleaseMouseCapture();
        
        // Si on n'a pas bougé, fermer la fenêtre
        if (!_hasMoved)
        {
            Close();
        }
        else
        {
            // Sauvegarder la position finale
            SavePosition();
        }
    }
    
    private void LoadSavedPosition()
    {
        try
        {
            var positions = GameOverlay.Models.PersistentStorageHelper.LoadJsonWithFallback<GameOverlay.Models.WindowPositions>("window_positions.json");
            
            if (positions?.SaleNotificationWindow != null)
            {
                Left = positions.SaleNotificationWindow.Left;
                Top = positions.SaleNotificationWindow.Top;
            }
            else
            {
                // Position par défaut : haut à droite
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                Loaded += (s, e) =>
                {
                    Left = screenWidth - ActualWidth - 20;
                    Top = 20;
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("SaleNotificationWindow", $"Erreur lors du chargement de la position: {ex.Message}");
            // Position par défaut en cas d'erreur
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            Loaded += (s, e) =>
            {
                Left = screenWidth - ActualWidth - 20;
                Top = 20;
            };
        }
    }
    
    private void SavePosition()
    {
        try
        {
            var positions = GameOverlay.Models.PersistentStorageHelper.LoadJsonWithFallback<GameOverlay.Models.WindowPositions>("window_positions.json");
            
            positions.SaleNotificationWindow = new GameOverlay.Models.WindowPosition
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            };
            
            GameOverlay.Models.PersistentStorageHelper.SaveJson("window_positions.json", positions);
        }
        catch (Exception ex)
        {
            Logger.Warning("SaleNotificationWindow", $"Erreur lors de la sauvegarde de la position: {ex.Message}");
        }
    }
}

