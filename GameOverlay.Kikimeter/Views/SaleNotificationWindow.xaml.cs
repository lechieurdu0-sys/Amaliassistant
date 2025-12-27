using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NAudio.Wave;
using Newtonsoft.Json;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Kikimeter.Services;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

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
    private bool _isDragging = false;
    private WpfPoint _dragStartPoint;
    private DateTime _mouseDownTime;
    private WaveOutEvent? _waveOut;
    private double _volume = 100;
    
    public SaleNotificationWindow()
    {
        InitializeComponent();
        Loaded += SaleNotificationWindow_Loaded;
        SourceInitialized += SaleNotificationWindow_SourceInitialized;
    }
    
    public SaleNotificationWindow(SaleInfo saleInfo, bool showAbsenceMessage = false, double notificationVolume = 100) : this()
    {
        // Normaliser le volume entre 0 et 100
        if (notificationVolume < 0) notificationVolume = 0;
        if (notificationVolume > 100) notificationVolume = 100;
        _volume = notificationVolume;
        
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
            // Utiliser HWND_TOPMOST avec les flags appropriés pour garantir la visibilité en plein écran
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            
            // Forcer un refresh pour s'assurer que la fenêtre est bien au-dessus
            System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    Topmost = false;
                    Topmost = true;
                });
            });
        }
    }
    
    private void SaleNotificationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Charger la position sauvegardée, sinon utiliser la position par défaut
        LoadWindowPosition();
        
        // Si aucune position sauvegardée, positionner la fenêtre en haut à droite de l'écran
        if (Left == 0 && Top == 0)
        {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen != null)
        {
            Left = screen.WorkingArea.Right - Width - 20;
            Top = 20;
            }
        }
        
        // Rendre la fenêtre cliquable mais pas transparente aux clics (pour pouvoir la fermer)
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            // Ne pas rendre transparent aux clics pour permettre la fermeture
            // SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT));
            
            // Forcer la fenêtre à rester au-dessus même pendant les jeux en plein écran
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            
            // S'assurer que la fenêtre est vraiment visible en forçant un refresh
            Topmost = false;
            Topmost = true;
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
                // Utiliser les API Windows waveOut pour permettre le contrôle du volume via le mélangeur Windows
                PlayWaveFile(soundPath);
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
    
    private void PlayWaveFile(string filePath)
    {
        // Utiliser NAudio pour jouer le son, ce qui garantit l'apparition dans le mélangeur de volume Windows
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using (var audioFile = new AudioFileReader(filePath))
                {
                    // Appliquer le volume depuis la configuration (0-100 %)
                    var volumeFactor = (float)(_volume / 100.0);
                    if (volumeFactor < 0f) volumeFactor = 0f;
                    if (volumeFactor > 1f) volumeFactor = 1f;
                    audioFile.Volume = volumeFactor;
                    
                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(audioFile);
                    _waveOut.Play();
                    
                    // Attendre la fin de la lecture
                    while (_waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }
            }
            catch
            {
                // En cas d'erreur, utiliser le son système par défaut
                try
                {
                    System.Media.SystemSounds.Exclamation.Play();
                }
                catch { }
            }
        });
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
        if (e.ChangedButton == MouseButton.Left)
        {
            // Démarrer le drag si c'est un clic gauche
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _mouseDownTime = DateTime.Now;
            CaptureMouse();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            // Fermer la notification si on clique avec le bouton droit
            CloseNotification();
        }
    }
    
    private void Window_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            WpfPoint currentPosition = e.GetPosition(this);
            Vector offset = currentPosition - _dragStartPoint;
            
            Left += offset.X;
            Top += offset.Y;
            
            // Sauvegarder la position après déplacement
            SaveWindowPosition();
        }
    }
    
    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            
            // Si c'était un simple clic (pas un drag), fermer la notification
            var clickDuration = DateTime.Now - _mouseDownTime;
            var dragDistance = (e.GetPosition(this) - _dragStartPoint).Length;
            
            if (clickDuration.TotalMilliseconds < 200 && dragDistance < 5)
            {
                // C'était un simple clic, fermer la notification
                CloseNotification();
            }
        }
    }
    
    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Fermer la notification si on clique avec le bouton droit
        CloseNotification();
    }
    
    private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Démarrer le drag depuis la bordure aussi
        if (e.ChangedButton == MouseButton.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _mouseDownTime = DateTime.Now;
            CaptureMouse();
            e.Handled = true;
        }
    }
    
    private void SaveWindowPosition()
    {
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant",
                "sale_notification_position.json"
            );
            
            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            
            var position = new
            {
                Left = Left,
                Top = Top
            };
            
            File.WriteAllText(configPath, JsonConvert.SerializeObject(position));
        }
        catch
        {
            // Ignorer les erreurs de sauvegarde
        }
    }
    
    private void LoadWindowPosition()
    {
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant",
                "sale_notification_position.json"
            );
            
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var position = JsonConvert.DeserializeObject<dynamic>(json);
                
                if (position != null)
                {
                    var leftValue = position.Left;
                    var topValue = position.Top;
                    
                    if (leftValue != null && topValue != null)
                    {
                        Left = (double)leftValue;
                        Top = (double)topValue;
                    }
                }
            }
        }
        catch
        {
            // Ignorer les erreurs de chargement, utiliser la position par défaut
        }
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
        
        // Arrêter et libérer les ressources audio
        if (_waveOut != null)
        {
            try
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            catch { }
        }
        
        base.OnClosed(e);
    }
}
