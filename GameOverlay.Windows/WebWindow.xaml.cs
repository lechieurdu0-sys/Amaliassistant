using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GameOverlay.Models;
using GameOverlay.Themes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;

namespace GameOverlay.Windows;

public partial class WebWindow : Window
{
    private Config? _config;
    private bool _isYouTube = false;

    public WebWindow(Config config)
    {
        InitializeComponent();
        _config = config;
        
        // Charger la position et la taille depuis la config
        LoadWindowSettings();
        
        // Initialiser WebView2
        InitializeWebView2();
        
        // Configurer la fenêtre
        SourceInitialized += WebWindow_SourceInitialized;
        
        // Synchroniser avec le thème (comme SettingsWindow)
        // Utiliser la couleur #FF6E5C2A (couleur SettingsWindow) au lieu du cyan
        var settingsWindowColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x5C, 0x2A));
        settingsWindowColor.Freeze();
        ApplyTheme(settingsWindowColor);
        ThemeManager.AccentColorChanged += ThemeManager_AccentColorChanged;
        
        // Charger l'opacité après que tous les éléments soient chargés
        Loaded += (s, e) =>
        {
            if (WebViewContainer != null && _config != null)
            {
                WebViewContainer.Opacity = Math.Max(0.1, Math.Min(1.0, _config.WebView2Opacity));
            }
            
            // S'assurer que le thème est appliqué après le chargement
            UpdateAccentBrush();
            
        };
        
        
        // Sauvegarder la position lors du déplacement (avec debounce pour éviter trop de sauvegardes)
        System.Windows.Threading.DispatcherTimer? saveTimer = null;
        LocationChanged += (s, e) =>
        {
            if (saveTimer == null)
            {
                saveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                saveTimer.Tick += (timerSender, timerE) =>
                {
                    saveTimer.Stop();
                    SaveWindowSettings();
                };
            }
            saveTimer.Stop();
            saveTimer.Start();
        };
        SizeChanged += (s, e) =>
        {
            if (saveTimer == null)
            {
                saveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                saveTimer.Tick += (timerSender, timerE) =>
                {
                    saveTimer.Stop();
                    SaveWindowSettings();
                };
            }
            saveTimer.Stop();
            saveTimer.Start();
            
            // Ajuster le zoom en fonction de la taille de la fenêtre
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateZoomFromWindowSize();
            }), System.Windows.Threading.DispatcherPriority.Background);
        };
    }
    
    private void ApplyTheme(System.Windows.Media.Brush accentBrush)
    {
        try
        {
            // Toujours utiliser la couleur #FF6E5C2A (couleur SettingsWindow) au lieu du cyan
            var themeColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x5C, 0x2A));
            
            themeColor.Freeze();
            Resources["CyanAccentBrush"] = themeColor;
            Resources["TextBrush"] = themeColor; // Même couleur pour le texte
            
            // Mettre à jour la bordure avec la couleur d'accent
            if (FindName("MainBorder") is System.Windows.Controls.Border mainBorder)
            {
                mainBorder.BorderBrush = themeColor;
            }
            
            // Mettre à jour le TextBox URL
            if (FindName("UrlTextBox") is TextBox urlTextBox)
            {
                urlTextBox.BorderBrush = themeColor;
                urlTextBox.Foreground = themeColor;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de l'application du thème: {ex.Message}");
        }
    }
    
    private void UpdateAccentBrush()
    {
        try
        {
            // Toujours utiliser la couleur #FF6E5C2A (couleur SettingsWindow) au lieu du cyan
            var accent = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x5C, 0x2A));
            accent.Freeze();
            
            Resources["CyanAccentBrush"] = accent;
            Resources["TextBrush"] = accent; // Même couleur pour le texte
            
            // Mettre à jour la bordure avec la nouvelle couleur d'accent
            if (FindName("MainBorder") is System.Windows.Controls.Border mainBorder)
            {
                mainBorder.BorderBrush = accent;
            }
            
            // Mettre à jour le TextBox URL
            if (FindName("UrlTextBox") is TextBox urlTextBox)
            {
                urlTextBox.BorderBrush = accent;
                urlTextBox.Foreground = accent;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de la mise à jour de la couleur d'accent: {ex.Message}");
        }
    }
    
    private void ThemeManager_AccentColorChanged(object? sender, AccentColorChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateAccentBrush);
    }

    private string GetDomainFolderName(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return "default";

            var uri = new Uri(url);
            string domain = uri.Host;
            
            // Retirer www. pour regrouper les variantes
            if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                domain = domain.Substring(4);
            }
            
            // Remplacer les caractères spéciaux par des underscores pour un nom de dossier valide
            domain = domain.Replace(".", "_");
            domain = System.Text.RegularExpressions.Regex.Replace(domain, @"[^a-zA-Z0-9_]", "_");
            
            return string.IsNullOrWhiteSpace(domain) ? "default" : domain;
        }
        catch
        {
            return "default";
        }
    }

    private async void InitializeWebView2()
    {
        try
        {
            // Déterminer le dossier de données basé sur l'URL du domaine pour mémoriser les sessions/cookies
            string baseUrl = _config?.WebWindowUrl ?? "https://www.google.com";
            string domainFolder = GetDomainFolderName(baseUrl);
            
            // Créer le dossier pour les données WebView2 basé sur le domaine
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Amaliassistant",
                "WebView2",
                domainFolder
            );
            
            if (!Directory.Exists(userDataFolder))
            {
                Directory.CreateDirectory(userDataFolder);
            }

            // Initialiser WebView2 avec le profil persistant
            // Option 1 : Runtime partagé (nécessite WebView2 Runtime installé)
            // var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            
            // Option 2 : Runtime fixe (embarqué - pas d'installation requise)
            // Décommenter et ajuster le chemin si vous voulez utiliser le runtime fixe
            string? browserExecutableFolder = null;
            
            // Chercher le runtime fixe dans le dossier de l'application
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string fixedRuntimePath = Path.Combine(appDirectory, "WebView2Runtime");
            if (Directory.Exists(fixedRuntimePath))
            {
                browserExecutableFolder = fixedRuntimePath;
                Logger.Info("WebWindow", $"Utilisation du runtime fixe WebView2 depuis: {browserExecutableFolder}");
            }
            else
            {
                Logger.Info("WebWindow", "Utilisation du runtime partagé WebView2 (nécessite WebView2 Runtime installé)");
            }
            
            var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder, userDataFolder);
            await WebView2Control.EnsureCoreWebView2Async(environment);
            
            // Configurer pour mémoriser l'historique et les cookies
            if (WebView2Control.CoreWebView2 != null)
            {
                // Activer la mémorisation des sessions et cookies (déjà activé par défaut avec userDataFolder)
                // Activer l'historique de navigation
                WebView2Control.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                
                // Activer les fonctionnalités PWA (Progressive Web App) pour une meilleure expérience
                // Cela permet aux sites web de se comporter comme des applications natives
                WebView2Control.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                WebView2Control.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                WebView2Control.CoreWebView2.Settings.AreDevToolsEnabled = false;
                
                // Injecter un script pour améliorer l'expérience PWA après le chargement de la page
                WebView2Control.CoreWebView2.DOMContentLoaded += async (s, e) =>
                {
                    try
                    {
                        // Injecter un manifeste web personnalisé pour améliorer l'expérience PWA
                        string pwaScript = @"
                            (function() {
                                // Vérifier si le site a déjà un manifeste
                                var existingManifest = document.querySelector('link[rel=""manifest""]');
                                
                                // Si pas de manifeste, en créer un pour améliorer l'expérience
                                if (!existingManifest) {
                                    var manifestLink = document.createElement('link');
                                    manifestLink.rel = 'manifest';
                                    manifestLink.href = 'data:application/json;charset=utf-8,' + encodeURIComponent(JSON.stringify({
                                        'name': document.title || window.location.hostname,
                                        'short_name': document.title || window.location.hostname,
                                        'display': 'standalone',
                                        'start_url': window.location.href,
                                        'theme_color': '#FF6E5C2A',
                                        'background_color': '#FF1A1A1A'
                                    }));
                                    document.head.appendChild(manifestLink);
                                }
                                
                                // Forcer le mode standalone pour une meilleure expérience
                                if (navigator.standalone === undefined) {
                                    Object.defineProperty(navigator, 'standalone', {
                                        get: function() { return true; }
                                    });
                                }
                                
                                // Améliorer la détection du mode PWA
                                if (window.matchMedia('(display-mode: standalone)').matches === false) {
                                    var style = document.createElement('style');
                                    style.textContent = '@media (display-mode: standalone) { body { } }';
                                    document.head.appendChild(style);
                                }
                            })();
                        ";
                        
                        await WebView2Control.CoreWebView2.ExecuteScriptAsync(pwaScript);
                        Logger.Debug("WebWindow", "Script PWA injecté avec succès");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("WebWindow", $"Erreur injection script PWA: {ex.Message}");
                    }
                };
                
                // Surveiller les changements d'URL pour afficher/masquer le bouton PIP
                WebView2Control.CoreWebView2.NavigationCompleted += (s, e) => {
                    Application.Current.Dispatcher.Invoke(() => {
                        UpdatePipButtonVisibility();
                        UpdateNavigationButtons();
                    });
                };
                
                // Initialiser le zoom selon la taille de la fenêtre
                UpdateZoomFromWindowSize();
                
                // Naviguer vers l'URL depuis la config
                string url = baseUrl;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    NavigateToUrlInternal(url);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de l'initialisation de WebView2: {ex.Message}");
            System.Windows.MessageBox.Show($"Erreur lors de l'initialisation du navigateur: {ex.Message}", 
                          "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void NavigateToUrlInternal(string url)
    {
        if (WebView2Control.CoreWebView2 == null) return;
        
        try
        {
            // Nettoyer l'URL
            url = url.Trim();
            
            // Si l'URL est vide, ne rien faire
            if (string.IsNullOrWhiteSpace(url))
                return;
            
            // Si ce n'est pas une URL valide (ne commence pas par http:// ou https://), faire une recherche Google
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Si ça ressemble à un domaine (contient un point), ajouter https://
                if (url.Contains(".") && !url.Contains(" "))
                {
                    url = "https://" + url;
                }
                else
                {
                    // Sinon, faire une recherche Google
                    url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
                }
            }
            
            // Naviguer vers l'URL
            WebView2Control.CoreWebView2.Navigate(url);
            
            // Détecter si c'est YouTube
            CheckIfYouTube(url);
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de la navigation vers {url}: {ex.Message}");
            System.Windows.MessageBox.Show($"Erreur lors de la navigation: {ex.Message}", 
                          "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void CheckIfYouTube(string url)
    {
        _isYouTube = url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || 
                     url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
        
        UpdatePipButtonVisibility();
    }
    
    private void UpdatePipButtonVisibility()
    {
        try
        {
            if (WebView2Control?.CoreWebView2 != null)
            {
                string currentUrl = WebView2Control.CoreWebView2.Source;
                bool isYouTube = IsYouTubeUrl(currentUrl);
                PipButton.Visibility = isYouTube ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur UpdatePipButtonVisibility: {ex.Message}");
        }
    }
    
    private bool IsYouTubeUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.Contains("youtube.com") || uri.Host.Contains("youtu.be");
        }
        catch
        {
            return false;
        }
    }

    private void CoreWebView2_HistoryChanged(object? sender, object e)
    {
        if (WebView2Control.CoreWebView2 == null) return;
        
        Dispatcher.Invoke(() =>
        {
            try
            {
                UpdateNavigationButtons();
            }
            catch (Exception ex)
            {
                Logger.Error("WebWindow", $"Erreur dans HistoryChanged: {ex.Message}");
            }
        });
    }
    
    private void UpdateNavigationButtons()
    {
        try
        {
            // Mettre à jour les boutons retour/avant en utilisant les méthodes natives de WebView2
            BackButton.IsEnabled = WebView2Control.CanGoBack;
            ForwardButton.IsEnabled = WebView2Control.CanGoForward;
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de la mise à jour des boutons de navigation: {ex.Message}");
        }
    }
    

    private void WebView2Control_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess && WebView2Control.CoreWebView2 != null)
        {
            // Configurer les options WebView2
            WebView2Control.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView2Control.CoreWebView2.Settings.AreDevToolsEnabled = false;
            
            // Activer la mémorisation des sessions (déjà fait via userDataFolder)
            // L'historique est automatiquement géré par WebView2
            
            // S'abonner aux événements de navigation
            WebView2Control.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
            
            // Désactiver la barre d'outils du développeur et les menus contextuels qui pourraient créer une croix
            WebView2Control.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            
            Logger.Info("WebWindow", "WebView2 initialisé avec succès - Sessions et historique mémorisés");
        }
        else
        {
            Logger.Error("WebWindow", $"Erreur lors de l'initialisation de WebView2: {e.InitializationException?.Message}");
        }
    }

    private void WebView2Control_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        Logger.Debug("WebWindow", $"Navigation vers: {e.Uri}");
        
        // Détecter si c'est YouTube
        CheckIfYouTube(e.Uri);
        
        // Mettre à jour l'URL affichée
        Dispatcher.Invoke(() =>
        {
            if (UrlTextBox != null)
            {
                UrlTextBox.Text = e.Uri;
            }
        });
    }

    private void WebView2Control_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            Logger.Error("WebWindow", $"Erreur de navigation: {e.WebErrorStatus}");
        }
        else
        {
            Logger.Debug("WebWindow", "Navigation terminée avec succès");
            
            // Mettre à jour l'historique interne et l'URL affichée
            Dispatcher.Invoke(() =>
            {
                if (WebView2Control.CoreWebView2 != null)
                {
                    string currentUrl = WebView2Control.CoreWebView2.Source;
                    if (UrlTextBox != null)
                    {
                        UrlTextBox.Text = currentUrl;
                    }
                    
                    // Mettre à jour les boutons de navigation (utilise l'historique natif de WebView2)
                    UpdateNavigationButtons();
                    
                    // Ajuster le viewport responsive après la navigation
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        await Task.Delay(300);
                        UpdateZoomFromWindowSize();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            });
        }
    }
    
    // Ajuster le zoom en fonction de la taille de la fenêtre (comme un smartphone - responsive)
    private async void UpdateZoomFromWindowSize()
    {
        try
        {
            if (WebView2Control?.CoreWebView2 == null)
                return;
            
            // Obtenir la largeur disponible pour le WebView (fenêtre - bordure - barre de titre)
            double availableWidth = Math.Max(300, this.Width - 4); // -4 pour les bordures
            double availableHeight = Math.Max(300, this.Height - 44); // -44 pour la barre de titre et bordures
            
            // Utiliser à la fois ZoomFactor ET viewport pour forcer le responsive
            try
            {
                // Calculer un zoom basé sur la largeur (pour les fenêtres très petites)
                double zoomFactor = 1.0;
                if (availableWidth < 800)
                {
                    // Pour les fenêtres verticales/étroites, forcer un zoom adaptatif
                    zoomFactor = Math.Max(0.5, availableWidth / 1200.0);
                }
                
                // Appliquer le zoom WebView2
                WebView2Control.ZoomFactor = zoomFactor;
                
                Logger.Debug("WebWindow", $"ZoomFactor défini à {zoomFactor:F2} pour {availableWidth}px");
            }
            catch (Exception zoomEx)
            {
                Logger.Warning("WebWindow", $"Impossible de définir ZoomFactor: {zoomEx.Message}");
            }
            
            // Injecter un viewport meta tag pour forcer le mode responsive (comme smartphone)
            string viewportScript = $@"
                (function() {{
                    try {{
                        // Supprimer tous les anciens viewports
                        var oldViewports = document.querySelectorAll('meta[name=""viewport""]');
                        oldViewports.forEach(function(vp) {{ vp.remove(); }});
                        
                        // Créer un nouveau viewport meta tag avec la largeur de la fenêtre
                        var viewport = document.createElement('meta');
                        viewport.name = 'viewport';
                        viewport.content = 'width={availableWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)}, initial-scale=1.0, maximum-scale=5.0, user-scalable=yes, shrink-to-fit=yes';
                        
                        var head = document.head || document.getElementsByTagName('head')[0] || document.documentElement;
                        head.insertBefore(viewport, head.firstChild);
                        
                        // Ajouter du CSS pour forcer la responsivité
                        var styleId = 'webwindow-responsive-style';
                        var existingStyle = document.getElementById(styleId);
                        if (existingStyle) {{
                            existingStyle.remove();
                        }}
                        
                        var style = document.createElement('style');
                        style.id = styleId;
                        style.innerHTML = 'html {{ width: 100% !important; max-width: 100% !important; overflow-x: hidden !important; }} body {{ width: 100% !important; max-width: 100% !important; overflow-x: auto !important; box-sizing: border-box !important; margin: 0 !important; padding: 0 !important; }} * {{ box-sizing: border-box !important; max-width: 100% !important; }}';
                        
                        head.appendChild(style);
                        
                        // Forcer plusieurs reflows pour s'assurer que ça fonctionne
                        if (document.body) {{
                            document.body.offsetHeight;
                            setTimeout(function() {{
                                document.body.offsetHeight;
                                if (window.innerWidth) {{ window.innerWidth; }}
                            }}, 100);
                        }}
                    }} catch(e) {{
                        console.error('Erreur viewport: ' + e.message);
                    }}
                }})();
            ";
            
            try
            {
                await WebView2Control.CoreWebView2.ExecuteScriptAsync(viewportScript);
                Logger.Debug("WebWindow", $"Viewport ajusté pour {availableWidth}x{availableHeight} (fenêtre: {this.Width}x{this.Height})");
                
                // Réessayer après plusieurs délais pour s'assurer que le viewport est bien appliqué
                await Task.Delay(300);
                await WebView2Control.CoreWebView2.ExecuteScriptAsync(viewportScript);
                await Task.Delay(500);
                await WebView2Control.CoreWebView2.ExecuteScriptAsync(viewportScript);
            }
            catch (Exception scriptEx)
            {
                Logger.Warning("WebWindow", $"Erreur injection viewport: {scriptEx.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de l'ajustement du viewport: {ex.Message}");
        }
    }

    private void LoadWindowSettings()
    {
        if (_config == null) return;

        // Charger la position
        if (_config.WebWindowX >= 0 && _config.WebWindowY >= 0)
        {
            this.Left = _config.WebWindowX;
            this.Top = _config.WebWindowY;
        }
        else
        {
            // Position par défaut (centre écran)
            this.Left = SystemParameters.PrimaryScreenWidth / 2 - 400;
            this.Top = SystemParameters.PrimaryScreenHeight / 2 - 300;
        }

        // Charger la taille
        if (_config.WebWindowWidth > 0 && _config.WebWindowHeight > 0)
        {
            this.Width = _config.WebWindowWidth;
            this.Height = _config.WebWindowHeight;
        }
        
        // Charger l'opacité de la fenêtre
        this.Opacity = Math.Max(0.1, Math.Min(1.0, _config.WebWindowOpacity));
        
        // Charger l'opacité du contenu WebView2
        if (WebViewContainer != null)
        {
            WebViewContainer.Opacity = Math.Max(0.1, Math.Min(1.0, _config.WebView2Opacity));
        }
        
        // Charger le style de la fenêtre
        ApplyWindowStyle();
    }
    
    private void ApplyWindowStyle()
    {
        if (_config == null) return;
        
        try
        {
            // Appliquer la couleur de fond de la fenêtre (sur le MainBorder si disponible)
            if (_config.WebWindowBackgroundEnabled)
            {
                var bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_config.WebWindowBackgroundColor);
                byte alpha = (byte)(bgColor.A * _config.WebWindowBackgroundOpacity);
                var bgWithOpacity = System.Windows.Media.Color.FromArgb(alpha, bgColor.R, bgColor.G, bgColor.B);
                var bgBrush = new System.Windows.Media.SolidColorBrush(bgWithOpacity);
                
                // Appliquer au Border principal si disponible, sinon à la fenêtre
                var mainBorder = this.FindName("MainBorder") as System.Windows.Controls.Border;
                if (mainBorder != null)
                {
                    mainBorder.Background = bgBrush;
                }
                else
                {
                    this.Background = bgBrush;
                }
            }
            else
            {
                var mainBorder = this.FindName("MainBorder") as System.Windows.Controls.Border;
                if (mainBorder != null)
                {
                    mainBorder.Background = System.Windows.Media.Brushes.Transparent;
                }
                this.Background = System.Windows.Media.Brushes.Transparent;
            }
            
            // Appliquer la couleur de la barre de titre
            var titleColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_config.WebWindowTitleBarColor);
            if (NavigationBar != null)
            {
                NavigationBar.Background = new System.Windows.Media.SolidColorBrush(titleColor);
            }
            if (Resources.Contains("TitleBarBrush"))
            {
                Resources["TitleBarBrush"] = new System.Windows.Media.SolidColorBrush(titleColor);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de l'application du style: {ex.Message}");
        }
    }

    private void SaveWindowSettings()
    {
        if (_config == null) return;

        try
        {
            _config.WebWindowX = (int)this.Left;
            _config.WebWindowY = (int)this.Top;
            _config.WebWindowWidth = this.Width;
            _config.WebWindowHeight = this.Height;
            _config.WebWindowOpacity = this.Opacity;
            _config.WebView2Opacity = WebViewContainer?.Opacity ?? 1.0;
            
            // Notifier MainWindow de sauvegarder la configuration
            NotifyConfigChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de la sauvegarde des paramètres: {ex.Message}");
        }
    }
    
    // Événement pour notifier MainWindow de sauvegarder la config
    public event Action? NotifyConfigChanged;



    // Gestionnaire pour déplacer la fenêtre via la barre de navigation (exactement comme WebOverlayWindow)
    private void NavigationBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            try
            {
                this.DragMove();
            }
            catch (Exception ex)
            {
                Logger.Error("WebWindow", $"Erreur DragMove: {ex.Message}");
            }
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (WebView2Control.CanGoBack)
            {
                WebView2Control.GoBack();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de la navigation arrière: {ex.Message}");
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (WebView2Control.CanGoForward)
            {
                WebView2Control.GoForward();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de la navigation avant: {ex.Message}");
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            WebView2Control.Reload();
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors du rafraîchissement: {ex.Message}");
        }
    }

    private async void PipButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (WebView2Control?.CoreWebView2 != null)
            {
                // Script JavaScript pour basculer le Picture-in-Picture sur YouTube (comme WebOverlayWindow)
                string script = @"
                    (function() {
                        // Trouver la vidéo YouTube
                        var video = document.querySelector('video');
                        if (video) {
                            // Vérifier si le navigateur supporte PIP
                            if (document.pictureInPictureEnabled) {
                                if (document.pictureInPictureElement) {
                                    // Si déjà en PIP, quitter
                                    document.exitPictureInPicture()
                                        .then(() => {
                                            console.log('PIP désactivé');
                                            return 'disabled';
                                        })
                                        .catch(err => {
                                            console.error('Erreur désactivation PIP:', err);
                                            return 'error';
                                        });
                                    return 'disabled';
                                } else {
                                    // Activer le PIP
                                    video.requestPictureInPicture()
                                        .then(() => {
                                            console.log('PIP activé');
                                            return 'enabled';
                                        })
                                        .catch(err => {
                                            console.error('Erreur activation PIP:', err);
                                            alert('Impossible d\'activer le PIP. Assurez-vous qu\'une vidéo est en cours de lecture.');
                                            return 'error';
                                        });
                                    return 'enabled';
                                }
                            } else {
                                alert('Picture-in-Picture non supporté par ce navigateur');
                                return 'not_supported';
                            }
                        } else {
                            alert('Aucune vidéo trouvée sur cette page. Allez sur une vidéo YouTube pour utiliser le PIP.');
                            return 'no_video';
                        }
                    })();
                ";
                
                string result = await WebView2Control.CoreWebView2.ExecuteScriptAsync(script);
                Logger.Debug("WebWindow", $"Script Picture-in-Picture exécuté, résultat: {result}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur PipButton_Click: {ex.Message}");
            System.Windows.MessageBox.Show($"Erreur lors du basculement du Picture-in-Picture: {ex.Message}", 
                          "Erreur PIP", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    
    // Gestionnaires pour le TextBox d'URL
    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            NavigateToUrlInternal(textBox.Text);
            textBox.SelectAll();
        }
    }
    
    private void UrlTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }
    
    private void UrlTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Mettre à jour l'URL affichée si elle a changé
        if (sender is TextBox textBox && WebView2Control.CoreWebView2 != null)
        {
            textBox.Text = WebView2Control.CoreWebView2.Source;
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Désabonner des événements de thème
        ThemeManager.AccentColorChanged -= ThemeManager_AccentColorChanged;
        base.OnClosed(e);
    }

    private void WebWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Exclure de Alt+Tab seulement (pas de WindowProc qui intercepte les clics)
        var helper = new WindowInteropHelper(this);
        ExcludeFromAltTab(helper.Handle);
    }
    

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
    
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;

    private void ExcludeFromAltTab(IntPtr hwnd)
    {
        try
        {
            uint extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            extendedStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur ExcludeFromAltTab: {ex.Message}");
        }
    }

    public void NavigateToUrl(string url)
    {
        NavigateToUrlInternal(url);
        if (_config != null)
        {
            _config.WebWindowUrl = url;
        }
    }
    
    // Gestionnaires d'opacité de la fenêtre
    private void Opacity100_Click(object sender, RoutedEventArgs e)
    {
        SetWindowOpacity(1.0);
    }
    
    private void Opacity80_Click(object sender, RoutedEventArgs e)
    {
        SetWindowOpacity(0.8);
    }
    
    private void Opacity60_Click(object sender, RoutedEventArgs e)
    {
        SetWindowOpacity(0.6);
    }
    
    private void SetWindowOpacity(double opacity)
    {
        this.Opacity = Math.Max(0.1, Math.Min(1.0, opacity));
        if (_config != null)
        {
            _config.WebWindowOpacity = this.Opacity;
            SaveWindowSettings();
        }
    }
    
    // Gestionnaires d'opacité du contenu WebView2
    private void ContentOpacity100_Click(object sender, RoutedEventArgs e)
    {
        SetContentOpacity(1.0);
    }
    
    private void ContentOpacity80_Click(object sender, RoutedEventArgs e)
    {
        SetContentOpacity(0.8);
    }
    
    private void ContentOpacity60_Click(object sender, RoutedEventArgs e)
    {
        SetContentOpacity(0.6);
    }
    
    private void SetContentOpacity(double opacity)
    {
        if (WebViewContainer != null)
        {
            WebViewContainer.Opacity = Math.Max(0.1, Math.Min(1.0, opacity));
            if (_config != null)
            {
                _config.WebView2Opacity = WebViewContainer.Opacity;
                SaveWindowSettings();
            }
        }
    }
    
    // Bouton d'opacité dans la barre
    private void OpacityButton_Click(object sender, RoutedEventArgs e)
    {
        if (StyleContextMenu != null)
        {
            StyleContextMenu.IsOpen = true;
        }
    }
    
    // Fenêtre de configuration du style
    private void StyleSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenStyleSettingsDialog();
    }
    
    private void OpenStyleSettingsDialog()
    {
        try
        {
            var dialog = new WebWindowStyleDialog(_config);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                // Appliquer les nouveaux paramètres de style
                ApplyWindowStyle();
                
                // Recharger l'opacité depuis la config
                if (_config != null)
                {
                    this.Opacity = Math.Max(0.1, Math.Min(1.0, _config.WebWindowOpacity));
                    if (WebViewContainer != null)
                    {
                        WebViewContainer.Opacity = Math.Max(0.1, Math.Min(1.0, _config.WebView2Opacity));
                    }
                }
                
                // Sauvegarder la configuration
                SaveWindowSettings();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("WebWindow", $"Erreur lors de l'ouverture du dialogue de style: {ex.Message}");
            System.Windows.MessageBox.Show($"Erreur lors de l'ouverture des paramètres de style: {ex.Message}",
                          "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
