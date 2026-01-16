using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Kikimeter.Services;
using GameOverlay.Models;
using GameOverlay.Themes;
using Newtonsoft.Json;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace GameOverlay.Kikimeter.Views;

public partial class LootWindow : Window, INotifyPropertyChanged
{
    private const string WindowPositionsFileName = "window_positions.json";
    private const string AppDataFolderName = "Amaliassistant";
    private const string LootFolderName = "Loot";
    private const string FavoritesFileName = "loot_favorites.json";

    private bool _lootFramesOpaque;
    private bool _isLoadingLootFramesOpaque;
    private string _currentSectionColorHex = "#FF000000";

    private bool _isDragging;
    private WpfPoint _mouseStartScreenPosition;
    private WpfPoint _windowStartPosition;
    private bool _isMinimized;
    private double _savedHeight;
    private bool _charactersPanelVisible;

    private LootCharacterDetector? _characterDetector;
    private LootTracker? _lootTracker;
    private LootManagementService? _lootManagementService;

    // SUPPRIMÉ : _allLootItems - utilise maintenant _lootManagementService.SessionLoot directement
    private readonly ObservableCollection<LootItem> _filteredLootItems = new();
    private readonly HashSet<string> _selectedCharacters = new(StringComparer.OrdinalIgnoreCase);
    // SUPPRIMÉ : _hiddenLootKeys - la suppression se fait maintenant directement dans le service

    private DispatcherTimer? _updateTimer;
    private bool _focusReturnPending;

    private string? _currentChatLogPath;
    private string? _currentKikimeterLogPath;

    public event EventHandler<ServerChangeDetectedEventArgs>? ServerSwitched;

    // Service partagé pour toute la session (singleton)
    private static LootManagementService? _sharedLootManagementService;
    private static readonly object _serviceLock = new object();
    
    public LootWindow()
    {
        InitializeComponent();

        DataContext = this;
        UpdateAccentBrushResource();

        ThemeManager.AccentColorChanged += ThemeManager_AccentColorChanged;
        ThemeManager.BubbleBackgroundColorChanged += ThemeManager_BubbleBackgroundColorChanged;

        InitializeWindow();
        SourceInitialized += LootWindow_SourceInitialized;

        LootItemsContainer.ItemsSource = _filteredLootItems;
        UpdateStatus();

        Loaded += (_, _) =>
        {
            LoadSectionBackgroundColor();
            HookFocusReturnHandlers();
        };

        Logger.Info("LootWindow", "LootWindow cree");
    }
    
    /// <summary>
    /// Obtient ou crée le service de gestion du loot partagé pour toute la session
    /// </summary>
    private static LootManagementService GetOrCreateSharedService()
    {
        if (_sharedLootManagementService == null)
        {
            lock (_serviceLock)
            {
                if (_sharedLootManagementService == null)
                {
                    _sharedLootManagementService = new LootManagementService();
                    Logger.Info("LootWindow", "Service de gestion du loot partagé créé (singleton)");
                }
            }
        }
        return _sharedLootManagementService;
    }

    public bool LootFramesOpaque
    {
        get => _lootFramesOpaque;
        set
        {
            if (_lootFramesOpaque != value)
            {
                _lootFramesOpaque = value;
                OnPropertyChanged(nameof(LootFramesOpaque));

                if (!_isLoadingLootFramesOpaque)
                {
                    SaveLootFramesOpacityPreference(value);
                }

                ApplySectionBackgroundColor(_currentSectionColorHex);
            }
        }
    }

    #region Window chrome / interactions

    private void LootWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WindowProc);

        ExcludeFromAltTab(helper.Handle);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
        {
            _isDragging = true;
            // Obtenir la position initiale de la souris en coordonnées d'écran via l'API Windows
            GetCursorPos(out var startPoint);
            _mouseStartScreenPosition = new WpfPoint(startPoint.X, startPoint.Y);
            _windowStartPosition = new WpfPoint(Left, Top);
            CaptureMouse();
            e.Handled = true;
        }
    }
    
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        // Obtenir la position actuelle de la souris en coordonnées d'écran via l'API Windows
        GetCursorPos(out var currentPoint);
        var currentScreenPos = new WpfPoint(currentPoint.X, currentPoint.Y);
        
        // Calculer le déplacement total depuis le début en coordonnées d'écran
        var offset = currentScreenPos - _mouseStartScreenPosition;
        
        // Appliquer le déplacement à la position initiale de la fenêtre
        // Utiliser UpdateLayout() pour forcer le rendu avant de déplacer
        var newLeft = _windowStartPosition.X + offset.X;
        var newTop = _windowStartPosition.Y + offset.Y;
        
        // Mettre à jour la position de manière atomique
        Left = newLeft;
        Top = newTop;
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();
        SaveWindowPositions();
        ScheduleFocusReturn();
    }


    // CharactersToggleButton supprimé - gestion des personnages maintenant dans SettingsWindow
    private void CharactersToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // Fonctionnalité désactivée - bouton supprimé de l'UI
    }

    // ClearLootButton supprimé - le reset est maintenant dans SettingsWindow
    private void ClearLootButton_Click(object sender, RoutedEventArgs e)
    {
        // Fonctionnalité désactivée - bouton supprimé de l'UI
    }

    public void SetZoom(double zoomValue)
    {
        if (WindowScale != null)
        {
            WindowScale.ScaleX = zoomValue;
            WindowScale.ScaleY = zoomValue;
            Logger.Debug("LootWindow", $"Zoom modifie: {(int)(zoomValue * 100)}%");
        }
    }

    // MinimizeButton supprimé - fonctionnalité désactivée
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // Fonctionnalité désactivée - bouton supprimé de l'UI
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        SaveWindowPositions();
    }


    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int WM_MOUSEACTIVATE = 0x0021;

        switch (msg)
        {
            case WM_MOUSEACTIVATE:
                ScheduleFocusReturn();
                break;
            case WM_NCHITTEST:
            {
            var screenPoint = new WpfPoint((short)(lParam.ToInt32() & 0xFFFF), (short)((lParam.ToInt32() >> 16) & 0xFFFF));
                var windowPoint = PointFromScreen(screenPoint);
                var rect = new Rect(RenderSize);

                const int resizeEdge = 5;

                if (windowPoint.Y >= rect.Height - resizeEdge)
                {
                    if (windowPoint.X <= resizeEdge)
                    {
                        handled = true;
                        return new IntPtr(16); // HTBOTTOMLEFT
                    }

                    if (windowPoint.X >= rect.Width - resizeEdge)
                    {
                        handled = true;
                        return new IntPtr(17); // HTBOTTOMRIGHT
                    }

                    handled = true;
                    return new IntPtr(15); // HTBOTTOM
                }

                if (windowPoint.Y <= resizeEdge)
                {
                    if (windowPoint.X <= resizeEdge)
                    {
                        handled = true;
                        return new IntPtr(13); // HTTOPLEFT
                    }

                    if (windowPoint.X >= rect.Width - resizeEdge)
                    {
                        handled = true;
                        return new IntPtr(14); // HTTOPRIGHT
                    }

                    handled = true;
                    return new IntPtr(12); // HTTOP
                }

                if (windowPoint.X <= resizeEdge)
                {
                    handled = true;
                    return new IntPtr(10); // HTLEFT
                }

                if (windowPoint.X >= rect.Width - resizeEdge)
                {
                    handled = true;
                    return new IntPtr(11); // HTRIGHT
                }

                handled = true;
                return new IntPtr(1); // HTCLIENT
            }
        }

        return IntPtr.Zero;
    }

    private void ExcludeFromAltTab(IntPtr handle)
    {
        const int GWL_EXSTYLE = -20;
        const uint WS_EX_TOOLWINDOW = 0x00000080;

        try
        {
            var style = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
        }
        catch
        {
            // best effort
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private void HookFocusReturnHandlers()
    {
        PreviewMouseLeftButtonUp += (_, _) => ScheduleFocusReturn();
        MouseLeftButtonUp += (_, _) => ScheduleFocusReturn();
        PreviewKeyUp += (_, _) => ScheduleFocusReturn();
        KeyUp += (_, _) => ScheduleFocusReturn();
        SizeChanged += (_, _) => ScheduleFocusReturn();
        LocationChanged += (_, _) => ScheduleFocusReturn();
    }

    private void ScheduleFocusReturn()
    {

        Dispatcher.BeginInvoke(ReturnFocusToGame, DispatcherPriority.Background);
    }

    private void ReturnFocusToGame()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                var candidates = Process
                    .GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.ProcessName)
                                && p.ProcessName.Contains("wakfu", StringComparison.OrdinalIgnoreCase)
                                && p.MainWindowHandle != IntPtr.Zero)
                    .OrderByDescending(p => p.MainWindowTitle.Length)
                    .ToList();

                var target = candidates.FirstOrDefault();
                if (target == null)
                {
                    return;
                }

                ShowWindow(target.MainWindowHandle, 9); // SW_RESTORE
                SetForegroundWindow(target.MainWindowHandle);
            }
            catch
            {
                // ignore
            }
        };

        timer.Start();
    }

    #endregion

    #region Initialisation / positions

    private void InitializeWindow()
    {
        LoadWindowPositions();
        LocationChanged += (_, _) => SaveWindowPositions();
        SizeChanged += (_, _) => SaveWindowPositions();
    }

    private void LoadWindowPositions()
    {
        try
        {
            var stored = PersistentStorageHelper.LoadJsonWithFallback<StoredWindowPositions>(WindowPositionsFileName);
            if (stored?.LootWindow == null)
            {
                return;
            }

            // Charger la position
            if (stored.LootWindow.Left > 0)
            {
                Left = stored.LootWindow.Left;
            }
            if (stored.LootWindow.Top > 0)
            {
                Top = stored.LootWindow.Top;
            }
            
            // Charger la taille si elle a été sauvegardée
            if (stored.LootWindow.Width > 0 && stored.LootWindow.Width >= MinWidth)
            {
                Width = stored.LootWindow.Width;
            }
            if (stored.LootWindow.Height > 0 && stored.LootWindow.Height >= MinHeight)
            {
                Height = stored.LootWindow.Height;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Impossible de charger les positions: {ex.Message}");
        }
    }

    private void SaveWindowPositions()
    {
        try
        {
            var stored = PersistentStorageHelper.LoadJsonWithFallback<StoredWindowPositions>(WindowPositionsFileName);

            stored.LootWindow = new StoredWindowPosition
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            };

            PersistentStorageHelper.SaveJson(WindowPositionsFileName, stored);
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Impossible de sauvegarder les positions: {ex.Message}");
        }
    }

    #endregion

    #region Log watching & data

    public void StartWatching(string chatLogPath, string? kikimeterLogPath = null)
    {
        _currentChatLogPath = chatLogPath;
        _currentKikimeterLogPath = kikimeterLogPath;

        StopWatchingInternal();

        try
        {
            // Charger la configuration existante avant de créer le détecteur pour préserver le personnage principal
            var existingConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant", "Loot", "loot_characters.json");
            string? existingMainCharacter = null;
            if (File.Exists(existingConfigPath))
            {
                try
                {
                    var existingJson = File.ReadAllText(existingConfigPath);
                    var existingConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<LootCharacterConfig>(existingJson);
                    if (existingConfig != null && !string.IsNullOrWhiteSpace(existingConfig.MainCharacter))
                    {
                        existingMainCharacter = existingConfig.MainCharacter;
                        Logger.Info("LootWindow", $"Personnage principal existant détecté: {existingMainCharacter}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning("LootWindow", $"Impossible de charger la configuration existante: {ex.Message}");
                }
            }
            
            _characterDetector = new LootCharacterDetector(chatLogPath, kikimeterLogPath);
            _characterDetector.CharactersChanged += OnCharactersChanged;
            _characterDetector.MainCharacterDetected += OnMainCharacterDetected;
            _characterDetector.ServerChanged += OnServerChanged;
            
            // Restaurer le personnage principal s'il existait avant
            if (!string.IsNullOrEmpty(existingMainCharacter))
            {
                var currentConfig = _characterDetector.GetConfig();
                if (string.IsNullOrWhiteSpace(currentConfig.MainCharacter) || 
                    !string.Equals(currentConfig.MainCharacter, existingMainCharacter, StringComparison.OrdinalIgnoreCase))
                {
                    _characterDetector.SetMainCharacter(existingMainCharacter);
                    Logger.Info("LootWindow", $"Personnage principal restauré: {existingMainCharacter}");
                }
            }
            
            UpdateCharactersList();

            // Obtenir ou créer le service de gestion du loot partagé (singleton pour toute la session)
            _lootManagementService = GetOrCreateSharedService();
            
            // Charger les favoris depuis le stockage (si pas déjà chargé)
            // Note: LoadFavorites peut être appelé plusieurs fois sans problème
            LoadFavorites();
            
            // S'abonner aux changements de la collection SessionLoot
            // IMPORTANT: Ne jamais désabonner car le service est partagé
            // La fenêtre peut être fermée/réouverte mais le service persiste
            _lootManagementService.SessionLoot.CollectionChanged -= SessionLoot_CollectionChanged; // Désabonner d'abord pour éviter les doubles abonnements
            _lootManagementService.SessionLoot.CollectionChanged += SessionLoot_CollectionChanged;
            
            // Initialiser le filtre avec les items existants (même si la fenêtre était fermée)
            ApplyFilters();
            
            // Créer le tracker avec le service
            _lootTracker = new LootTracker(chatLogPath, _lootManagementService);
            // Les événements LootItemAdded/Updated/Removed ne sont plus nécessaires
            // car on s'abonne directement à SessionLoot.CollectionChanged

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            Logger.Info("LootWindow", $"Surveillance initialisee sur {chatLogPath}");
        }
        catch (Exception ex)
        {
            Logger.Error("LootWindow", $"Erreur lors de l'initialisation des trackers: {ex.Message}");
        }
    }

    public void RegisterCombatPlayers(IEnumerable<string> playerNames)
    {
        if (playerNames == null)
        {
            return;
        }

        void Sync()
        {
            var playersList = playerNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (playersList.Count == 0)
            {
                return;
            }

            _characterDetector?.RegisterCombatPlayers(playersList);
            UpdateCharactersList();
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke((Action)Sync);
        }
        else
        {
            Sync();
        }
    }

    private void OnServerChanged(object? sender, ServerChangeDetectedEventArgs e)
    {
        void ApplyServerChange()
        {
            var serverLabel = string.IsNullOrWhiteSpace(e.ServerName)
                ? "Déconnexion du serveur"
                : $"Changement de serveur ({e.ServerName})";

            Logger.Info("LootWindow", $"Réinitialisation suite à un {serverLabel}.");

            ResetAllLoot(serverLabel);
            ServerSwitched?.Invoke(this, e);
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke((Action)ApplyServerChange);
        }
        else
        {
            ApplyServerChange();
        }
    }

    private void StopWatchingInternal()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= UpdateTimer_Tick;
            _updateTimer = null;
        }

        if (_lootTracker != null)
        {
            // Les événements LootItemAdded/Updated/Removed ne sont plus nécessaires
            // car on utilise directement SessionLoot.CollectionChanged
            _lootTracker.Dispose();
            _lootTracker = null;
        }
        
        // IMPORTANT : Ne PAS détruire _lootManagementService
        // C'est la source unique de vérité pour toute la session
        // Il doit persister même si la fenêtre est fermée
        // Seul le reset serveur peut le vider
        
        // IMPORTANT : Ne PAS désabonner de SessionLoot.CollectionChanged
        // Car le service est partagé et peut être utilisé par d'autres instances
        // On se désabonnera seulement si on détruit vraiment la fenêtre (ce qui n'arrive pas avec Hide())

        if (_characterDetector != null)
        {
            _characterDetector.CharactersChanged -= OnCharactersChanged;
            _characterDetector.MainCharacterDetected -= OnMainCharacterDetected;
            _characterDetector.ServerChanged -= OnServerChanged;
            _characterDetector.Dispose();
            _characterDetector = null;
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_lootTracker == null && _characterDetector == null)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                _lootTracker?.ManualRead();
            }
            catch (Exception ex)
            {
                Logger.Error("LootWindow", $"Lecture manuelle loot: {ex.Message}");
            }
        });

        Task.Run(() =>
        {
            try
            {
                _characterDetector?.ManualScan();
            }
            catch (Exception ex)
            {
                Logger.Error("LootWindow", $"Scan manuel personnages: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Gère les changements dans la collection SessionLoot (source unique de vérité)
    /// </summary>
    private void SessionLoot_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Recalculer le filtre quand la collection change
            ApplyFilters();
            UpdateStatus();
        });
    }
    
    // SUPPRIMÉ : OnLootItemAdded, OnLootItemUpdated, OnLootItemRemoved
    // La collection SessionLoot est maintenant la source unique de vérité
    // Les changements sont gérés via CollectionChanged

    private void OnCharactersChanged(object? sender, List<string> characters)
    {
        Dispatcher.Invoke(UpdateCharactersList);
    }

    private void OnMainCharacterDetected(object? sender, string mainCharacter)
    {
        Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrWhiteSpace(mainCharacter))
            {
                return;
            }

            _lootTracker?.SetMainCharacter(mainCharacter);

            // CORRECTION MAJEURE : Ne plus recréer la collection
            // La collection SessionLoot est la source unique de vérité et ne doit JAMAIS être vidée
            // Seul le changement de nom du personnage principal est géré ici
            // Les items avec "Vous" seront mis à jour automatiquement par LootTracker.SetMainCharacter()
            
            // Appliquer les favoris aux items existants dans SessionLoot
            if (_lootManagementService != null)
            {
                var favoriteKeysFromService = _lootManagementService.SaveFavorites();
                foreach (var item in _lootManagementService.SessionLoot)
                {
                    var key = $"{item.CharacterName}|{item.ItemName}";
                    item.IsFavorite = favoriteKeysFromService.Contains(key);
                }
            }

            ApplyFilters();
            UpdateStatus();
            UpdateCharactersList();
        });
    }

    private void ApplyFilters()
    {
        _filteredLootItems.Clear();

        if (_lootManagementService == null)
            return;

        string? mainCharacter = _characterDetector?.GetConfig().MainCharacter;

        // S'assurer qu'au moins le personnage principal est sélectionné
        if (_selectedCharacters.Count == 0)
        {
            if (!string.IsNullOrEmpty(mainCharacter))
            {
                _selectedCharacters.Add(mainCharacter);
                Logger.Info("LootWindow", $"Personnage principal {mainCharacter} ajouté automatiquement dans ApplyFilters");
            }
            else
            {
                // Si aucun personnage principal n'est défini, utiliser "Vous" comme fallback
                _selectedCharacters.Add("Vous");
                Logger.Info("LootWindow", "Aucun personnage principal défini, utilisation de 'Vous' comme fallback");
            }
        }

        // Trier: favoris en premier, puis par date de dernière obtention
        var sortedItems = _lootManagementService.SessionLoot
            .OrderByDescending(i => i.IsFavorite)
            .ThenByDescending(i => i.LastObtained)
            .ToList();

        foreach (var item in sortedItems)
        {
            var actualName = item.CharacterName;
            // Si l'item a "Vous" comme nom et qu'un personnage principal est défini, utiliser le nom du personnage
            if (string.Equals(actualName, "Vous", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(mainCharacter))
            {
                actualName = mainCharacter;
            }
            // Sinon, garder "Vous" tel quel si aucun personnage principal n'est défini

            // Vérifier si l'item a été supprimé par l'utilisateur
            // (Le service gère maintenant la suppression, mais on vérifie quand même pour sécurité)
            if (_lootManagementService.IsUserDeleted(item.CharacterName, item.ItemName))
            {
                // Ne pas afficher les items supprimés par l'utilisateur
                continue;
            }

            // Filtrer par personnage sélectionné
            // Ne pas filtrer les favoris même si la quantité est 0
            if (_selectedCharacters.Contains(actualName))
            {
                _filteredLootItems.Add(item);
            }
        }
    }

    private void UpdateStatus()
    {
        StatusText.Text = _selectedCharacters.Count == 0
            ? "Aucun joueur selectionne"
            : $"{_filteredLootItems.Count} type(s) | {_filteredLootItems.Sum(i => i.Quantity)} items | {_selectedCharacters.Count} joueur(s)";
    }

    private void DeleteLootItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not LootItem lootItem)
        {
            return;
        }

        if (_lootManagementService == null)
            return;

        // Le service gère la protection contre la suppression des favoris
        bool deleted = _lootManagementService.DeleteLootByUser(lootItem.CharacterName, lootItem.ItemName);
        
        if (deleted)
        {
            // Le filtre sera mis à jour automatiquement via CollectionChanged
            UpdateStatus();
            ScheduleFocusReturn();
        }
    }

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not LootItem lootItem)
        {
            return;
        }

        if (_lootManagementService == null)
            return;

        // Le service gère la logique complète des favoris
        _lootManagementService.ToggleFavorite(lootItem.CharacterName, lootItem.ItemName);

        // Sauvegarder les favoris
        SaveFavorites();
        
        // Le filtre sera mis à jour automatiquement via CollectionChanged
        UpdateStatus();
        ScheduleFocusReturn();
    }

    private void LoadFavorites()
    {
        if (_lootManagementService == null)
            return;
            
        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName, LootFolderName);
            Directory.CreateDirectory(appData);
            
            var favoritesPath = Path.Combine(appData, FavoritesFileName);
            if (!File.Exists(favoritesPath))
            {
                return;
            }

            var json = File.ReadAllText(favoritesPath);
            var favorites = JsonConvert.DeserializeObject<List<string>>(json);
            
            if (favorites != null)
            {
                var favoriteKeysSet = new HashSet<string>(favorites, StringComparer.OrdinalIgnoreCase);
                _lootManagementService.LoadFavorites(favoriteKeysSet);
                Logger.Info("LootWindow", $"Favoris chargés: {favoriteKeysSet.Count} items");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Impossible de charger les favoris: {ex.Message}");
        }
    }

    private void SaveFavorites()
    {
        if (_lootManagementService == null)
            return;
            
        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName, LootFolderName);
            Directory.CreateDirectory(appData);
            
            var favoritesPath = Path.Combine(appData, FavoritesFileName);
            var favorites = _lootManagementService.SaveFavorites().ToList();
            var json = JsonConvert.SerializeObject(favorites, Formatting.Indented);
            File.WriteAllText(favoritesPath, json);
            
            Logger.Debug("LootWindow", $"Favoris sauvegardés: {favorites.Count} items");
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Impossible de sauvegarder les favoris: {ex.Message}");
        }
    }

    private void ResetAllLoot(string reason)
    {
        Logger.Info("LootWindow", $"Reset complet: {reason}");

        // Utiliser le service pour le reset
        _lootManagementService?.Reset();
        
        _characterDetector?.ResetCharacterStorage(rehydrateAfterReset: false, suppressServerEvents: true);

        _filteredLootItems.Clear();
        _selectedCharacters.Clear();
        // Ne pas effacer les favoris lors d'un reset (le service les conserve)

        UpdateCharactersList();
        UpdateStatus();
    }

    #endregion

    #region Personnages UI

    // UpdateCharactersList désactivé - gestion des personnages maintenant dans SettingsWindow
    private void UpdateCharactersList()
    {
        // CharactersListContainer supprimé - gestion maintenant dans SettingsWindow
        if (_characterDetector == null)
        {
            return;
        }

        // La liste des personnages est maintenant gérée dans SettingsWindow
        var previousSelection = new HashSet<string>(_selectedCharacters, StringComparer.OrdinalIgnoreCase);
        _selectedCharacters.Clear();
        // CharactersListContainer.Children.Clear(); // Supprimé

        var config = _characterDetector.GetConfig();
        var mainCharacter = config.MainCharacter;
        
        // S'assurer que le personnage principal est toujours visible dans la configuration
        // ET qu'il est bien défini comme personnage principal
        if (!string.IsNullOrEmpty(mainCharacter))
        {
            // S'assurer que le personnage principal existe dans Characters
            if (!config.Characters.ContainsKey(mainCharacter))
            {
                config.Characters[mainCharacter] = true;
                _characterDetector.SetCharacterVisibility(mainCharacter, true);
                Logger.Info("LootWindow", $"Personnage principal {mainCharacter} ajouté avec visible=true dans UpdateCharactersList");
            }
            // S'assurer que le personnage principal est visible
            else if (!config.Characters[mainCharacter])
            {
                config.Characters[mainCharacter] = true;
                _characterDetector.SetCharacterVisibility(mainCharacter, true);
                Logger.Info("LootWindow", $"Personnage principal {mainCharacter} forcé à visible=true dans UpdateCharactersList");
            }
            
            // S'assurer que MainCharacter est bien défini dans la config
            if (!string.Equals(config.MainCharacter, mainCharacter, StringComparison.OrdinalIgnoreCase))
            {
                _characterDetector.SetMainCharacter(mainCharacter);
                Logger.Info("LootWindow", $"Personnage principal {mainCharacter} restauré dans la configuration");
            }
        }
        
        var orderedCharacters = config.Characters.Keys.OrderBy(name => name).ToList();
        var myCharacters = config.MyCharacters ?? new List<string>();

        if (!string.IsNullOrEmpty(mainCharacter))
        {
            CreateCharacterItem(mainCharacter, isMyCharacter: true, isMain: true, config);
        }

        foreach (var character in myCharacters.Where(c => !string.Equals(c, mainCharacter, StringComparison.OrdinalIgnoreCase)))
        {
            if (config.Characters.ContainsKey(character))
            {
                CreateCharacterItem(character, isMyCharacter: true, isMain: false, config);
            }
        }

        foreach (var character in orderedCharacters)
        {
            if (string.Equals(character, mainCharacter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (myCharacters.Contains(character, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            CreateCharacterItem(character, isMyCharacter: false, isMain: false, config);
        }

        foreach (var name in previousSelection)
        {
            _selectedCharacters.Add(name);
        }

        // S'assurer qu'au moins le personnage principal est sélectionné si aucun personnage n'est sélectionné
        if (_selectedCharacters.Count == 0)
        {
            if (!string.IsNullOrEmpty(mainCharacter))
            {
                _selectedCharacters.Add(mainCharacter);
                Logger.Info("LootWindow", $"Personnage principal {mainCharacter} ajouté automatiquement car aucun personnage n'était sélectionné");
            }
            else
            {
                // Si aucun personnage principal n'est défini, utiliser "Vous" comme fallback
                _selectedCharacters.Add("Vous");
                Logger.Info("LootWindow", "Aucun personnage principal défini, utilisation de 'Vous' comme fallback dans UpdateCharactersList");
            }
        }

        ApplyFilters();
        UpdateStatus();
        UpdateCharacterItemsBackground();
    }

    private void CreateCharacterItem(string characterName, bool isMyCharacter, bool isMain, LootCharacterConfig config)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(ThemeManager.AccentColor),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(5, 4, 5, 4),
            Background = (Brush)Resources["SectionBackgroundBrush"],
            MinHeight = 46
        };

        var row = new Grid
        {
            Margin = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Button CreateIconButton(string text, string tooltip, Brush foreground, double fontSize = 18)
        {
            return new Button
            {
                Content = text,
                ToolTip = tooltip,
                Foreground = foreground,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = 24,
                MinHeight = 24
            };
        }

        var accentBrush = new SolidColorBrush(ThemeManager.AccentColor);
        var inactiveBrush = new SolidColorBrush(Color.FromArgb(255, 140, 140, 140));

        var starBrush = isMain ? Brushes.Gold : accentBrush;
        var starButton = CreateIconButton("★", "Définir comme personnage principal", starBrush, isMain ? 24 : 18);
        Grid.SetColumn(starButton, 0);
        row.Children.Add(starButton);

        starButton.Click += (_, _) =>
        {
            try
            {
                _characterDetector?.SetMainCharacter(characterName);
                UpdateCharactersList();
                ScheduleFocusReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("LootWindow", $"Impossible de définir {characterName} comme personnage principal: {ex.Message}");
            }
        };

        // Le personnage principal doit toujours être coché
        bool shouldBeChecked = isMain || (config.Characters.TryGetValue(characterName, out var visible) && visible);
        
        // S'assurer que le personnage principal est dans config.Characters avec true
        if (isMain && (!config.Characters.ContainsKey(characterName) || !config.Characters[characterName]))
        {
            config.Characters[characterName] = true;
            _characterDetector?.SetCharacterVisibility(characterName, true);
            Logger.Info("LootWindow", $"Personnage principal {characterName} forcé à visible=true");
        }

        var checkbox = new CheckBox
        {
            Content = characterName,
            IsChecked = shouldBeChecked,
            IsEnabled = !isMain,
            Foreground = accentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(4, 0, 4, 0)
        };
        Grid.SetColumn(checkbox, 1);
        row.Children.Add(checkbox);

        checkbox.Checked += (_, _) =>
        {
            _selectedCharacters.Add(characterName);
            _characterDetector?.SetCharacterVisibility(characterName, true);
            ApplyFilters();
            UpdateStatus();
            ScheduleFocusReturn();
        };

        checkbox.Unchecked += (_, _) =>
        {
            if (isMain)
            {
                // Forcer le personnage principal à rester coché
                checkbox.IsChecked = true;
                // S'assurer que la configuration est à jour
                if (_characterDetector != null)
                {
                    var currentConfig = _characterDetector.GetConfig();
                    if (!currentConfig.Characters.ContainsKey(characterName) || !currentConfig.Characters[characterName])
                    {
                        _characterDetector.SetCharacterVisibility(characterName, true);
                        Logger.Info("LootWindow", $"Personnage principal {characterName} forcé à visible=true après tentative de décochage");
                    }
                }
                ScheduleFocusReturn();
                return;
            }

            _selectedCharacters.Remove(characterName);
            _characterDetector?.SetCharacterVisibility(characterName, false);
            ApplyFilters();
            UpdateStatus();
            ScheduleFocusReturn();
        };

        if (checkbox.IsChecked == true)
        {
            _selectedCharacters.Add(characterName);
        }

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        Grid.SetColumn(actionsPanel, 2);

        if (!isMain)
        {
            var nopButton = CreateIconButton("✕", "Retirer définitivement ce personnage", inactiveBrush);
            actionsPanel.Children.Add(nopButton);
            nopButton.Click += (_, _) =>
            {
                try
                {
                    _selectedCharacters.Remove(characterName);
                    _characterDetector?.RemoveCharacter(characterName);
                    UpdateCharactersList();
                    ApplyFilters();
                    UpdateStatus();
                    ScheduleFocusReturn();
                }
                catch (Exception ex)
                {
                    Logger.Error("LootWindow", $"Impossible de retirer {characterName}: {ex.Message}");
                }
            };
        }

        row.Children.Add(actionsPanel);

        border.Child = row;
        // CharactersListContainer.Children.Add(border); // Supprimé - gestion maintenant dans SettingsWindow
    }

    // UpdateCharacterItemsBackground désactivé - gestion des personnages maintenant dans SettingsWindow
    private void UpdateCharacterItemsBackground()
    {
        // CharactersListContainer supprimé - gestion maintenant dans SettingsWindow
        // if (Resources["SectionBackgroundBrush"] is not SolidColorBrush brush)
        // {
        //     return;
        // }
        //
        // foreach (var border in CharactersListContainer.Children.OfType<Border>())
        // {
        //     border.Background = new SolidColorBrush(brush.Color);
        // }
    }

    #endregion

    #region Theme & colors

    private void UpdateAccentBrushResource()
    {
        try
        {
            Resources["CyanAccentBrush"] = ThemeManager.AccentBrush;
        }
        catch
        {
            // ignore
        }
    }

    private void ThemeManager_AccentColorChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateAccentBrushResource();
            UpdateCharactersList();
        }));
    }

    private void ThemeManager_BubbleBackgroundColorChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateCharacterItemsBackground();
        }));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void LoadSectionBackgroundColor()
    {
        try
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName, "config.json");
            if (!File.Exists(configPath))
            {
                ApplySectionBackgroundColor(_currentSectionColorHex);
                return;
            }

            var json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<Config>(json);
            if (config == null)
            {
                ApplySectionBackgroundColor(_currentSectionColorHex);
                return;
            }

            _isLoadingLootFramesOpaque = true;
            LootFramesOpaque = config.LootFramesOpaque;
            _isLoadingLootFramesOpaque = false;

            if (!string.IsNullOrEmpty(config.KikimeterSectionBackgroundColor))
            {
                ApplySectionBackgroundColor(config.KikimeterSectionBackgroundColor);
            }
            else
            {
                ApplySectionBackgroundColor(_currentSectionColorHex);
            }

            // Ne pas définir le Background si KikimeterWindowBackgroundEnabled est false
            // Cela permet au background défini dans le XAML (ImageBrush) d'être visible
            if (config.KikimeterWindowBackgroundEnabled && !string.IsNullOrEmpty(config.KikimeterWindowBackgroundColor))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(config.KikimeterWindowBackgroundColor);
                    color.A = (byte)(config.KikimeterWindowBackgroundOpacity * 255);
                    var brush = new SolidColorBrush(color);
                    Background = brush;
                    // MainBorder reste transparent pour laisser voir le background de la fenêtre
                }
                catch
                {
                    // En cas d'erreur, ne pas définir de background pour laisser celui du XAML
                }
            }
            // Si KikimeterWindowBackgroundEnabled est false, on laisse le background du XAML (ImageBrush)
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Erreur chargement couleur: {ex.Message}");
        }
    }

    public void ApplySectionBackgroundColor(string colorHex)
    {
        try
        {
            _currentSectionColorHex = string.IsNullOrWhiteSpace(colorHex) ? _currentSectionColorHex : colorHex;
            var color = (Color)ColorConverter.ConvertFromString(_currentSectionColorHex);

            if (LootFramesOpaque)
            {
                color.A = 255;
            }
            else if (color.A == 255)
            {
                color.A = 128;
            }

            var brush = new SolidColorBrush(color);
            Resources["SectionBackgroundBrush"] = brush;
            UpdateCharacterItemsBackground();
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Couleur invalide {colorHex}: {ex.Message}");
        }
    }

    private void SaveSectionBackgroundColor(string colorHex)
    {
        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);
            Directory.CreateDirectory(appData);

            var configPath = Path.Combine(appData, "config.json");
            Config config;

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                config = new Config();
            }

            config.KikimeterSectionBackgroundColor = colorHex;
            var updated = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, updated);
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Impossible de sauvegarder la couleur: {ex.Message}");
        }
    }

    private void SaveLootFramesOpacityPreference(bool isOpaque)
    {
        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);
            Directory.CreateDirectory(appData);

            var configPath = Path.Combine(appData, "config.json");
            Config config;

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                config = new Config();
            }

            config.LootFramesOpaque = isOpaque;
            if (string.IsNullOrEmpty(config.KikimeterSectionBackgroundColor))
            {
                config.KikimeterSectionBackgroundColor = _currentSectionColorHex;
            }

            var updated = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, updated);
            Logger.Info("LootWindow", $"Préférence d'opacité des cadres sauvegardée ({(isOpaque ? "opaque" : "semi-transparent")}).");
        }
        catch (Exception ex)
        {
            Logger.Warning("LootWindow", $"Impossible de sauvegarder l'opacité des cadres: {ex.Message}");
        }
    }


    #endregion

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        StopWatchingInternal();
        ThemeManager.AccentColorChanged -= ThemeManager_AccentColorChanged;
        ThemeManager.BubbleBackgroundColorChanged -= ThemeManager_BubbleBackgroundColorChanged;
    }

    private sealed class StoredWindowPositions
    {
        public StoredWindowPosition? LootWindow { get; set; }
        public StoredWindowPosition? KikimeterWindow { get; set; }
        public StoredWindowPosition? KikimeterWindowVertical { get; set; }
        public StoredWindowPosition? KikimeterWindowHorizontal { get; set; }
        [Newtonsoft.Json.JsonExtensionData]
        public Dictionary<string, Newtonsoft.Json.Linq.JToken>? ExtensionData { get; set; }
    }

    private sealed class StoredWindowPosition
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}

