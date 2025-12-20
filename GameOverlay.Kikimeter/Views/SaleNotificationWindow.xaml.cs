using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Media;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Kikimeter.Services;

namespace GameOverlay.Kikimeter.Views;

public partial class SaleNotificationWindow : Window
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int GWL_EXSTYLE = -20;
    
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    
    private System.Windows.Threading.DispatcherTimer? _autoCloseTimer;
    
    public SaleNotificationWindow()
    {
        InitializeComponent();
        Loaded += SaleNotificationWindow_Loaded;
        SourceInitialized += SaleNotificationWindow_SourceInitialized;
    }
    
    public SaleNotificationWindow(SaleInfo saleInfo, bool showAbsenceMessage = false) : this()
    {
        // Initialiser le contenu de la notification
        if (MessageTextRun != null)
        {
            if (showAbsenceMessage)
            {
                MessageTextRun.Text = $"Pendant votre absence, vous avez vendu {saleInfo.ItemCount} objet{(saleInfo.ItemCount > 1 ? "s" : "")} pour ";
            }
            else
            {
                MessageTextRun.Text = $"Vous avez vendu {saleInfo.ItemCount} objet{(saleInfo.ItemCount > 1 ? "s" : "")} pour ";
            }
        }
        
        if (AbsenceTextRun != null)
        {
            AbsenceTextRun.Text = $" {saleInfo.TotalKamas:N0} kamas";
        }
    }
    
    private void SaleNotificationWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Forcer la fenêtre à rester au-dessus même pendant les jeux en plein écran
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            // Utiliser SetWindowPos pour forcer la fenêtre à rester au-dessus
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }
    
    private void SaleNotificationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Positionner la fenêtre en haut à droite de l'écran
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen != null)
        {
            Left = screen.WorkingArea.Right - Width - 20;
            Top = 20;
        }
        
        // Rendre la fenêtre cliquable mais pas transparente aux clics (pour pouvoir la fermer)
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            // Ne pas rendre transparent aux clics pour permettre la fermeture
            // SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT));
            
            // Forcer la fenêtre à rester au-dessus même pendant les jeux en plein écran
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
        
        // Jouer le son de notification
        PlayNotificationSound();
        
        // Animation d'apparition
        Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
        BeginAnimation(OpacityProperty, fadeIn);
        
        // Auto-fermeture après 5 secondes
        _autoCloseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoCloseTimer.Tick += (s, args) =>
        {
            _autoCloseTimer?.Stop();
            CloseNotification();
        };
        _autoCloseTimer.Start();
    }
    
    private void PlayNotificationSound()
    {
        try
        {
            // Chercher le fichier son dans plusieurs emplacements possibles
            string[] possiblePaths = new[]
            {
                // Chemin relatif depuis l'exécutable publié (dans le dossier publish)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sounds", "sale_notification.wav"),
                // Chemin depuis l'assembly (pour le développement)
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "Resources", "Sounds", "sale_notification.wav"),
                // Chemin depuis le dossier de l'application (pour les installations)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "Resources", "Sounds", "sale_notification.wav"),
                // Chemin alternatif depuis l'assembly (si l'assembly est dans un sous-dossier)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameOverlay.Kikimeter", "Resources", "Sounds", "sale_notification.wav")
            };
            
            string? soundPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    soundPath = path;
                    break;
                }
            }
            
            if (soundPath != null)
            {
                using (var player = new SoundPlayer(soundPath))
                {
                    player.Play();
                }
            }
            else
            {
                // Si aucun fichier son n'est trouvé, utiliser le son système par défaut
                System.Media.SystemSounds.Exclamation.Play();
            }
        }
        catch
        {
            // En cas d'erreur, utiliser le son système par défaut
            try
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch
            {
                // Ignorer les erreurs de lecture de son
            }
        }
    }
    
    public void StopAutoCloseTimer()
    {
        _autoCloseTimer?.Stop();
    }
    
    public void StartAutoCloseTimer()
    {
        if (_autoCloseTimer == null)
        {
            _autoCloseTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoCloseTimer.Tick += (s, args) =>
            {
                _autoCloseTimer?.Stop();
                CloseNotification();
            };
        }
        _autoCloseTimer.Start();
    }
    
    
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Fermer la notification si on clique dessus
        CloseNotification();
    }
    
    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Fermer la notification si on clique dessus avec le bouton droit
        CloseNotification();
    }
    
    private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Fermer la notification si on clique sur la bordure
        CloseNotification();
    }
    
    private void CloseNotification()
    {
        if (_autoCloseTimer != null)
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer = null;
        }
        
        // Animation de disparition
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
        fadeOut.Completed += (s, e) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer?.Stop();
        base.OnClosed(e);
    }
}
