using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Kikimeter.Services;
using GameOverlay.Models;
using GameOverlay.Themes;
using Logger = GameOverlay.Models.Logger;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using Newtonsoft.Json;
using WpfPoint = System.Windows.Point;

namespace GameOverlay.Kikimeter.Views;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly Config _config;
    private readonly Action<Config> _onConfigChanged;
    private readonly Func<IEnumerable<string>>? _getCurrentPlayers;
    private readonly Action? _onResetLootRequested;
    private const string ManualOrderConfigFileName = "kikimeter_manual_order.json";
    private const string LootCharactersFileName = "loot_characters.json";
    private static readonly string LootConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Amaliassistant",
        "Loot");
    
    // Onglet Ordre des joueurs
    public ObservableCollection<PlayerOrderItem> Items { get; }
    private ObservableCollection<PlayerOrderItem>? _originalItems;
    
    // Onglet Gestion des personnages
    private LootCharacterDetector? _characterDetector;
    private readonly SolidColorBrush _sectionBackgroundBrush;
    private DispatcherTimer? _characterListRefreshTimer;
    private bool _isCharacterManagementTabVisible = false;
    
    // Onglet Chemins de logs
    private string? _kikimeterLogPath;
    private string? _lootChatLogPath;
    
    // Démarrage automatique
    private bool _startWithWindows;
    
    public string? KikimeterLogPath
    {
        get => _kikimeterLogPath;
        set
        {
            if (_kikimeterLogPath != value)
            {
                _kikimeterLogPath = value;
                OnPropertyChanged(nameof(KikimeterLogPath));
            }
        }
    }
    
    public string? LootChatLogPath
    {
        get => _lootChatLogPath;
        set
        {
            if (_lootChatLogPath != value)
            {
                _lootChatLogPath = value;
                OnPropertyChanged(nameof(LootChatLogPath));
            }
        }
    }
    
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows != value)
            {
                _startWithWindows = value;
                OnPropertyChanged(nameof(StartWithWindows));
            }
        }
    }

    public SettingsWindow(
        Config config,
        Action<Config> onConfigChanged,
        IEnumerable<string>? currentPlayers = null,
        Func<IEnumerable<string>>? getCurrentPlayers = null,
        System.Windows.Media.Brush? accentBrush = null,
        System.Windows.Media.Brush? sectionBackgroundBrush = null,
        Action? onResetLootRequested = null)
    {
        InitializeComponent();
        
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _onConfigChanged = onConfigChanged ?? throw new ArgumentNullException(nameof(onConfigChanged));
        _getCurrentPlayers = getCurrentPlayers;
        _onResetLootRequested = onResetLootRequested;
        
        // Initialiser l'ordre des joueurs
        var playersList = currentPlayers?.ToList() ?? new List<string>();
        Logger.Info("SettingsWindow", $"Initialisation avec {playersList.Count} joueurs: {string.Join(", ", playersList)}");
        Items = new ObservableCollection<PlayerOrderItem>(
            playersList.Select((name, index) => new PlayerOrderItem(name, index))
        );
        RenumberItems();
        PopulatePlayersFromStoredData();
        Logger.Info("SettingsWindow", $"Items initialisé avec {Items.Count} éléments");
        
        // Initialiser les couleurs
        var accent = accentBrush ?? ThemeManager.AccentBrush;
        var section = sectionBackgroundBrush?.CloneCurrentValue() as SolidColorBrush
                     ?? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00));
        section.Freeze();
        _sectionBackgroundBrush = section;
        
        ApplyTheme(accent);
        Resources["SectionBackgroundBrush"] = _sectionBackgroundBrush;
        
        // Initialiser les chemins de logs
        KikimeterLogPath = config.KikimeterLogPath;
        LootChatLogPath = config.LootChatLogPath;
        
        // Initialiser le démarrage automatique (vérifier le registre)
        StartWithWindows = IsStartupEnabled();
        
        DataContext = this;
        
        // Forcer le binding du ListBox immédiatement après InitializeComponent
        if (PlayerListBox != null)
        {
            PlayerListBox.ItemsSource = Items;
            Logger.Info("SettingsWindow", $"ItemsSource défini explicitement dans le constructeur: {Items.Count} éléments");
        }
        
        ThemeManager.AccentColorChanged += ThemeManager_AccentColorChanged;
        Loaded += SettingsWindow_Loaded;
        Closing += SettingsWindow_Closing;
        IsVisibleChanged += SettingsWindow_IsVisibleChanged;
        
        // Initialiser la gestion des personnages si un chemin de log est disponible
        TryInitializeCharacterDetector();
    }
    
    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Info("SettingsWindow", $"SettingsWindow_Loaded: Items.Count = {Items.Count}, PlayerListBox.Items.Count = {PlayerListBox?.Items.Count ?? 0}");
        
        // Toujours forcer le binding pour s'assurer qu'il fonctionne
        if (PlayerListBox != null)
        {
            PlayerListBox.ItemsSource = null;
            PlayerListBox.ItemsSource = Items;
            Logger.Info("SettingsWindow", $"ItemsSource forcé: {Items.Count} éléments, PlayerListBox.Items.Count = {PlayerListBox.Items.Count}");
        }
        
        // Mettre à jour la liste des joueurs au chargement si getCurrentPlayers est disponible
        if (_getCurrentPlayers != null)
        {
            Logger.Info("SettingsWindow", "Mise à jour de la liste des joueurs depuis getCurrentPlayers");
            UpdatePlayersList();
        }
        
        if (PlayerListBox != null && PlayerListBox.Items.Count > 0)
        {
            PlayerListBox.SelectedIndex = 0;
        }
        UpdateButtonStates();
    }
    
    private void SettingsWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Empêcher la fermeture réelle, masquer à la place
        e.Cancel = true;
        Hide();
        
        // Nettoyer le détecteur de personnages
        StopCharacterListRefreshTimer();
        CleanupCharacterDetector();
        ThemeManager.AccentColorChanged -= ThemeManager_AccentColorChanged;
    }
    
    private void SettingsWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            // Fenêtre visible : toujours initialiser et démarrer le timer pour la gestion des personnages
            TryInitializeCharacterDetector();
            RefreshCharactersListFromFile();
            StartCharacterListRefreshTimer();
            _isCharacterManagementTabVisible = true;
            
            // Toujours forcer le binding pour s'assurer qu'il fonctionne
            if (PlayerListBox != null)
            {
                PlayerListBox.ItemsSource = null;
                PlayerListBox.ItemsSource = Items;
                Logger.Info("SettingsWindow", $"ItemsSource forcé (IsVisibleChanged): {Items.Count} éléments");
            }
            
            // Mettre à jour la liste des joueurs quand la fenêtre devient visible
            UpdatePlayersList();
        }
        else
        {
            // Fenêtre cachée : arrêter le timer
            StopCharacterListRefreshTimer();
            _isCharacterManagementTabVisible = false;
        }
    }
    
    private void ThemeManager_AccentColorChanged(object? sender, AccentColorChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateAccentBrush);
    }
    
    private void ApplyTheme(System.Windows.Media.Brush accentBrush)
    {
        var accentClone = accentBrush.CloneCurrentValue();
        accentClone.Freeze();
        Resources["CyanAccentBrush"] = accentClone;
    }
    
    private void UpdateAccentBrush()
    {
        var accent = ThemeManager.AccentBrush?.CloneCurrentValue();
        if (accent == null)
            return;

        accent.Freeze();
        Resources["CyanAccentBrush"] = accent;
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Gérer aussi le cas où c'est une Image qui appelle cette méthode
        if (sender is System.Windows.Controls.Image || sender is System.Windows.Controls.Button)
        {
            Hide();
        }
    }
    
    #region Déplacement de la fenêtre
    
    private bool _isDragging = false;
    private WpfPoint _dragStartPoint;
    
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }
    }
    
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        TitleBar_MouseLeftButtonDown(sender, e);
    }
    
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            WpfPoint currentPosition = e.GetPosition(this);
            Vector offset = currentPosition - _dragStartPoint;
            Left += offset.X;
            Top += offset.Y;
        }
    }
    
    private void DragHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        OnMouseMove(e);
    }
    
    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
        }
    }
    
    private void DragHandle_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OnMouseLeftButtonUp(e);
    }
    
    #endregion
    
    #region Ordre des joueurs
    
    private void MoveUpButton_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(-1);
    
    private void MoveDownButton_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(1);
    
    private void MoveSelectedItem(int delta)
    {
        int index = PlayerListBox.SelectedIndex;
        if (index < 0)
            return;

        int newIndex = index + delta;
        if (newIndex < 0 || newIndex >= Items.Count)
            return;

        Items.Move(index, newIndex);
        RenumberItems();
        PlayerListBox.SelectedIndex = newIndex;
        PlayerListBox.ScrollIntoView(PlayerListBox.SelectedItem);
    }
    
    private void RenumberItems()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].Order = i;
        }
    }

    private void PopulatePlayersFromStoredData()
    {
        try
        {
            var storedNames = LoadOrderedNamesFromManualOrder();
            if (storedNames.Count == 0)
            {
                storedNames = LoadCharacterNamesFromConfig();
            }

            if (storedNames.Count == 0)
            {
                return;
            }

            var existing = new HashSet<string>(Items.Select(item => item.Name), StringComparer.OrdinalIgnoreCase);

            if (Items.Count == 0)
            {
                for (int i = 0; i < storedNames.Count; i++)
                {
                    Items.Add(new PlayerOrderItem(storedNames[i], i));
                }
            }
            else
            {
                int nextOrder = Items.Count;
                foreach (var name in storedNames)
                {
                    if (!existing.Contains(name))
                    {
                        Items.Add(new PlayerOrderItem(name, nextOrder++));
                    }
                }
            }

            RenumberItems();
        }
        catch (Exception ex)
        {
            Logger.Warning("SettingsWindow", $"PopulatePlayersFromStoredData: {ex.Message}");
        }
    }

    private void UpdateEmptyListMessage()
    {
        if (FindName("EmptyListMessage") is TextBlock emptyMessage)
        {
            emptyMessage.Visibility = Items.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
    
    private void PlayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }
    
    private void UpdateButtonStates()
    {
        int index = PlayerListBox.SelectedIndex;
        MoveUpButton.IsEnabled = index > 0;
        MoveDownButton.IsEnabled = index >= 0 && index < Items.Count - 1;
    }
    
    private void ResetPlayerOrder_Click(object sender, RoutedEventArgs e)
    {
        ResetPlayerOrder();
    }
    
    public void ResetPlayerOrder()
    {
        if (_getCurrentPlayers != null)
        {
            var currentPlayers = _getCurrentPlayers()?.ToList() ?? new List<string>();
            Items.Clear();
            foreach (var player in currentPlayers)
            {
                Items.Add(new PlayerOrderItem(player, Items.Count));
            }
            RenumberItems();
            if (Items.Count > 0)
            {
                PlayerListBox.SelectedIndex = 0;
            }
        }
        else
        {
            // Si pas de getCurrentPlayers, vider la liste
            Items.Clear();
            RenumberItems();
        }
    }
    
    public void ResetCharacterList()
    {
        // Réinitialiser le stockage des personnages dans le détecteur
        if (_characterDetector != null)
        {
            _characterDetector.ResetCharacterStorage(rehydrateAfterReset: false, suppressServerEvents: false);
        }
        else
        {
            ResetCharacterConfigFile();
        }
        
        // Mettre à jour la liste des personnages
        UpdateCharactersList();
    }
    
    private void ConfirmPlayerOrder_Click(object sender, RoutedEventArgs e)
    {
        // Sauvegarder l'ordre actuel
        _originalItems = new ObservableCollection<PlayerOrderItem>(
            Items.Select(item => new PlayerOrderItem(item.Name, item.Order))
        );
        
        // Déclencher le callback pour appliquer l'ordre immédiatement
        _onConfigChanged(_config);
        
        MessageBox.Show(
            "L'ordre des joueurs a été appliqué au Kikimeter.",
            "Ordre sauvegardé",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    
    public IReadOnlyList<string> GetOrderedNames() => Items.Select(item => item.Name).ToList();
    
    public void UpdatePlayersList()
    {
        try
        {
            var currentPlayers = _getCurrentPlayers?.Invoke()?.ToList() ?? new List<string>();
            if (currentPlayers.Count == 0)
            {
                currentPlayers = LoadOrderedNamesFromManualOrder();
                if (currentPlayers.Count == 0)
                {
                    currentPlayers = LoadCharacterNamesFromConfig();
                }
            }
            
            Logger.Info("SettingsWindow", $"UpdatePlayersList: {currentPlayers.Count} joueurs récupérés: {string.Join(", ", currentPlayers)}");
            
            // Sauvegarder l'ordre actuel des joueurs existants
            var existingOrderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Items)
            {
                existingOrderMap[item.Name] = item.Order;
            }

            if (existingOrderMap.Count == 0)
            {
                foreach (var kvp in LoadManualOrderMap())
                {
                    if (!existingOrderMap.ContainsKey(kvp.Key))
                    {
                        existingOrderMap[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            // Vider et reconstruire la liste
            Items.Clear();
            
            // Ajouter les joueurs existants dans leur ordre actuel, puis les nouveaux
            var existingPlayers = currentPlayers.Where(p => existingOrderMap.ContainsKey(p)).ToList();
            var newPlayers = currentPlayers.Where(p => !existingOrderMap.ContainsKey(p)).ToList();
            
            // Trier les joueurs existants par leur ordre actuel
            existingPlayers = existingPlayers.OrderBy(p => existingOrderMap[p]).ToList();
            
            // Ajouter les joueurs existants
            foreach (var player in existingPlayers)
            {
                Items.Add(new PlayerOrderItem(player, existingOrderMap[player]));
            }
            
            // Ajouter les nouveaux joueurs à la fin
            int nextOrder = Items.Count > 0 ? Items.Max(item => item.Order) + 1 : 0;
            foreach (var player in newPlayers)
            {
                Items.Add(new PlayerOrderItem(player, nextOrder++));
            }
            
            RenumberItems();
            PopulatePlayersFromStoredData();
            
            // Forcer la mise à jour du binding et de l'affichage
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (PlayerListBox != null)
                {
                    // Forcer la mise à jour de l'ItemsSource
                    PlayerListBox.ItemsSource = null;
                    PlayerListBox.ItemsSource = Items;
                    
                    // Forcer la mise à jour visuelle
                    PlayerListBox.UpdateLayout();
                    PlayerListBox.InvalidateVisual();
                    
                    UpdateEmptyListMessage();
                    
                    if (Items.Count > 0)
                    {
                        if (PlayerListBox.SelectedIndex < 0)
                        {
                            PlayerListBox.SelectedIndex = 0;
                        }
                        UpdateButtonStates();
                        Logger.Info("SettingsWindow", $"UpdatePlayersList: {Items.Count} joueurs affichés dans PlayerListBox");
                    }
                    else
                    {
                        Logger.Warning("SettingsWindow", "UpdatePlayersList: Aucun joueur à afficher");
                    }
                }
            }), DispatcherPriority.Loaded);
            
            Logger.Info("SettingsWindow", $"UpdatePlayersList terminé: {Items.Count} joueurs dans la liste");
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur dans UpdatePlayersList: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    #endregion
    
    #region Gestion des personnages
    
    private void TryInitializeCharacterDetector()
    {
        if (_characterDetector != null)
            return;
            
        // Utiliser le chemin de log Loot si disponible, sinon Kikimeter
        var chatLogPath = LootChatLogPath;
        var kikimeterLogPath = KikimeterLogPath;
        
        // Si aucun chemin n'est configuré, essayer la détection automatique
        if (string.IsNullOrWhiteSpace(chatLogPath))
        {
            if (!string.IsNullOrWhiteSpace(kikimeterLogPath))
            {
                chatLogPath = WakfuLogFinder.FindChatLogFile(kikimeterLogPath);
            }
            
            if (string.IsNullOrWhiteSpace(chatLogPath))
            {
                var mainLogPath = WakfuLogFinder.FindFirstLogFile();
                if (!string.IsNullOrWhiteSpace(mainLogPath))
                {
                    kikimeterLogPath = mainLogPath;
                    chatLogPath = WakfuLogFinder.FindChatLogFile(mainLogPath);
                }
            }
        }
        
        if (string.IsNullOrWhiteSpace(chatLogPath) || !File.Exists(chatLogPath))
        {
            // Pas de fichier de log disponible, ne pas initialiser
            return;
        }
        
        try
        {
            LootCharacterDetector.EnsureConfigFileExists();
            _characterDetector = new LootCharacterDetector(chatLogPath, kikimeterLogPath);
            _characterDetector.CharactersChanged += OnCharactersChanged;
            _characterDetector.MainCharacterDetected += OnMainCharacterDetected;
            _characterDetector.ManualScan();
            UpdateCharactersList();
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur lors de l'initialisation du détecteur de personnages: {ex.Message}");
        }
    }
    
    private void CleanupCharacterDetector()
    {
        StopCharacterListRefreshTimer();
        
        if (_characterDetector != null)
        {
            try
            {
                _characterDetector.CharactersChanged -= OnCharactersChanged;
                _characterDetector.MainCharacterDetected -= OnMainCharacterDetected;
                _characterDetector.Dispose();
            }
            catch { }
            _characterDetector = null;
        }
    }
    
    private void StartCharacterListRefreshTimer()
    {
        StopCharacterListRefreshTimer();
        
        if (!_isCharacterManagementTabVisible || _characterDetector == null)
            return;
        
        _characterListRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2) // Rafraîchir toutes les 2 secondes
        };
        _characterListRefreshTimer.Tick += (s, e) =>
        {
            if (_isCharacterManagementTabVisible && _characterDetector != null)
            {
                RefreshCharactersListFromFile();
            }
        };
        _characterListRefreshTimer.Start();
        Logger.Info("SettingsWindow", "Timer de rafraîchissement de la liste des personnages démarré");
    }
    
    private void StopCharacterListRefreshTimer()
    {
        if (_characterListRefreshTimer != null)
        {
            _characterListRefreshTimer.Stop();
            _characterListRefreshTimer = null;
            Logger.Info("SettingsWindow", "Timer de rafraîchissement de la liste des personnages arrêté");
        }
    }
    
    /// <summary>
    /// Recharge la liste des personnages directement depuis le fichier de configuration
    /// sans passer par les événements, pour garantir qu'on a toujours les données à jour
    /// </summary>
    private void RefreshCharactersListFromFile()
    {
        if (!_isCharacterManagementTabVisible)
            return;
        
        try
        {
            Logger.Debug("SettingsWindow", "RefreshCharactersListFromFile: Rechargement depuis le fichier");
            // Recharger directement depuis le fichier sans passer par les événements
            UpdateCharactersList();
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur dans RefreshCharactersListFromFile: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private void OnCharactersChanged(object? sender, List<string> characters)
    {
        try
        {
            Logger.Info("SettingsWindow", $"OnCharactersChanged appelé avec {characters?.Count ?? 0} personnages: {string.Join(", ", characters ?? new List<string>())}");
            
            // Utiliser BeginInvoke pour éviter les blocages et permettre les mises à jour asynchrones
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isCharacterManagementTabVisible && _characterDetector != null)
                {
                    UpdateCharactersList();
                    Logger.Info("SettingsWindow", "UpdateCharactersList terminé après OnCharactersChanged");
                }
            }), DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur dans OnCharactersChanged: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private void OnMainCharacterDetected(object? sender, string mainCharacter)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateCharactersList();
        });
    }
    
    private void UpdateCharactersList()
    {
        if (CharactersListContainer == null)
        {
            Logger.Warning("SettingsWindow", "UpdateCharactersList: CharactersListContainer est null");
            return;
        }

        try
        {
            Logger.Info("SettingsWindow", "UpdateCharactersList: Début de la mise à jour");
            CharactersListContainer.Children.Clear();

            var config = GetActiveCharacterConfig();
            var mainCharacter = config.MainCharacter;
            var myCharacters = config.MyCharacters ?? new List<string>();
            var manualCharacters = config.ManualCharacters ?? new List<string>();
            
            Logger.Info("SettingsWindow", $"Config chargée: MainCharacter={mainCharacter}, MyCharacters={myCharacters.Count}, ManualCharacters={manualCharacters.Count}, Characters={config.Characters.Count}");
            
            // Créer un set de tous les personnages déjà ajoutés pour éviter les doublons
            var addedCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Ajouter le personnage principal en premier
            if (!string.IsNullOrEmpty(mainCharacter))
            {
                Logger.Info("SettingsWindow", $"Ajout du personnage principal: {mainCharacter}");
                CreateCharacterItem(mainCharacter, isMyCharacter: true, isMain: true, config);
                addedCharacters.Add(mainCharacter);
            }

            // 2. Ajouter tous les personnages "myCharacters" (même s'ils ne sont pas dans config.Characters)
            foreach (var character in myCharacters)
            {
                if (!addedCharacters.Contains(character))
                {
                    Logger.Info("SettingsWindow", $"Ajout d'un personnage MyCharacters: {character}");
                    var isMain = string.Equals(character, mainCharacter, StringComparison.OrdinalIgnoreCase);
                    CreateCharacterItem(character, isMyCharacter: true, isMain: isMain, config);
                    addedCharacters.Add(character);
                }
            }

            // 3. Ajouter tous les personnages manuels
            foreach (var character in manualCharacters)
            {
                if (!addedCharacters.Contains(character) && !string.IsNullOrWhiteSpace(character))
                {
                    Logger.Info("SettingsWindow", $"Ajout d'un personnage manuel: {character}");
                    var isMyChar = myCharacters.Contains(character, StringComparer.OrdinalIgnoreCase);
                    var isMain = string.Equals(character, mainCharacter, StringComparison.OrdinalIgnoreCase);
                    CreateCharacterItem(character, isMyCharacter: isMyChar, isMain: isMain, config);
                    addedCharacters.Add(character);
                }
            }

            // 4. Ajouter tous les autres personnages de config.Characters
            foreach (var character in config.Characters.Keys.OrderBy(name => name))
            {
                if (!addedCharacters.Contains(character))
                {
                    Logger.Info("SettingsWindow", $"Ajout d'un personnage de config.Characters: {character}");
                    var isMyChar = myCharacters.Contains(character, StringComparer.OrdinalIgnoreCase);
                    var isMain = string.Equals(character, mainCharacter, StringComparison.OrdinalIgnoreCase);
                    CreateCharacterItem(character, isMyCharacter: isMyChar, isMain: isMain, config);
                    addedCharacters.Add(character);
                }
            }
            
            Logger.Info("SettingsWindow", $"UpdateCharactersList terminé: {addedCharacters.Count} personnages affichés ({string.Join(", ", addedCharacters)})");
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur dans UpdateCharactersList: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private void CreateCharacterItem(string characterName, bool isMyCharacter, bool isMain, LootCharacterConfig config)
    {
        // Couleurs harmonisées avec le reste de l'interface
        var borderColor = System.Windows.Media.Color.FromRgb(0x6E, 0x5C, 0x2A); // #FF6E5C2A
        var textColor = System.Windows.Media.Color.FromRgb(0x4E, 0x42, 0x1F); // #FF4E421F
        
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(5, 1, 5, 1),
            Background = _sectionBackgroundBrush,
            MinHeight = 28
        };

        var row = new Grid
        {
            Margin = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var accentBrush = new SolidColorBrush(textColor);
        var inactiveBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 140, 140, 140));

        var starBrush = isMain ? System.Windows.Media.Brushes.Gold : new SolidColorBrush(borderColor);
        var starButton = new System.Windows.Controls.Button
        {
            Content = "★",
            ToolTip = "Définir comme personnage principal",
            Foreground = starBrush,
            FontSize = isMain ? 18 : 14,
            FontWeight = FontWeights.Bold,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(0, 0, 6, 0),
            MinWidth = 20,
            MinHeight = 20
        };
        Grid.SetColumn(starButton, 0);
        row.Children.Add(starButton);

        starButton.Click += (_, _) =>
        {
            try
            {
                if (TrySetMainCharacter(characterName))
                {
                    Logger.Info("SettingsWindow", $"Personnage principal défini: {characterName}");
                    UpdateCharactersList();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("SettingsWindow", $"Impossible de définir {characterName} comme personnage principal: {ex.Message}\n{ex.StackTrace}");
            }
        };

        // Le personnage principal doit toujours être coché
        bool shouldBeChecked = isMain || (config.Characters.TryGetValue(characterName, out var visible) && visible);
        
        // S'assurer que le personnage principal est dans config.Characters avec true
        if (isMain && (!config.Characters.ContainsKey(characterName) || !config.Characters[characterName]))
        {
            config.Characters[characterName] = true;
            _characterDetector?.SetCharacterVisibility(characterName, true);
            Logger.Info("SettingsWindow", $"Personnage principal {characterName} forcé à visible=true");
        }
        
        // Créer un style personnalisé pour la checkbox avec images
        var checkboxStyle = new System.Windows.Style(typeof(System.Windows.Controls.CheckBox));
        
        // Template personnalisé avec images
        var template = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.CheckBox));
        
        // StackPanel horizontal pour l'image et le contenu
        var stackPanel = new FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel));
        stackPanel.SetValue(System.Windows.Controls.StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
        
        // Grid pour contenir les deux images (une visible à la fois)
        var imageContainer = new FrameworkElementFactory(typeof(System.Windows.Controls.Grid));
        
        // Image checkbox vide (par défaut visible)
        var imageEmpty = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
        imageEmpty.Name = "ImageEmpty";
        imageEmpty.SetValue(System.Windows.Controls.Image.SourceProperty, new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/GameOverlay.Kikimeter;component/Views/Assets/utilities/checkbox_empty.png")));
        imageEmpty.SetValue(System.Windows.Controls.Image.WidthProperty, 20.0);
        imageEmpty.SetValue(System.Windows.Controls.Image.HeightProperty, 20.0);
        imageEmpty.SetValue(System.Windows.Controls.Image.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        imageEmpty.SetValue(System.Windows.Controls.Image.MarginProperty, new Thickness(0, 0, 6, 0));
        imageEmpty.SetValue(System.Windows.Controls.Image.VisibilityProperty, System.Windows.Visibility.Visible);
        
        // Image checkbox pleine (cachée par défaut)
        var imageFull = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
        imageFull.Name = "ImageFull";
        imageFull.SetValue(System.Windows.Controls.Image.SourceProperty, new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/GameOverlay.Kikimeter;component/Views/Assets/utilities/checkbox_full.png")));
        imageFull.SetValue(System.Windows.Controls.Image.WidthProperty, 20.0);
        imageFull.SetValue(System.Windows.Controls.Image.HeightProperty, 20.0);
        imageFull.SetValue(System.Windows.Controls.Image.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        imageFull.SetValue(System.Windows.Controls.Image.MarginProperty, new Thickness(0, 0, 6, 0));
        imageFull.SetValue(System.Windows.Controls.Image.VisibilityProperty, System.Windows.Visibility.Collapsed);
        
        imageContainer.AppendChild(imageEmpty);
        imageContainer.AppendChild(imageFull);
        stackPanel.AppendChild(imageContainer);
        
        // ContentPresenter pour le texte
        var contentPresenter = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
        contentPresenter.SetValue(System.Windows.Controls.ContentPresenter.ContentProperty, new TemplateBindingExtension(System.Windows.Controls.ContentControl.ContentProperty));
        contentPresenter.SetValue(System.Windows.Controls.ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(System.Windows.Controls.ContentControl.ContentTemplateProperty));
        contentPresenter.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        
        stackPanel.AppendChild(contentPresenter);
        
        template.VisualTree = stackPanel;
        
        // Trigger pour IsChecked = True
        var checkedTrigger = new System.Windows.Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Image.VisibilityProperty, System.Windows.Visibility.Collapsed, "ImageEmpty"));
        checkedTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Image.VisibilityProperty, System.Windows.Visibility.Visible, "ImageFull"));
        template.Triggers.Add(checkedTrigger);
        
        // Trigger pour IsChecked = False
        var uncheckedTrigger = new System.Windows.Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = false };
        uncheckedTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Image.VisibilityProperty, System.Windows.Visibility.Visible, "ImageEmpty"));
        uncheckedTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Image.VisibilityProperty, System.Windows.Visibility.Collapsed, "ImageFull"));
        template.Triggers.Add(uncheckedTrigger);
        
        checkboxStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.TemplateProperty, template));
        
        var checkbox = new System.Windows.Controls.CheckBox
        {
            Content = characterName,
            IsChecked = shouldBeChecked,
            IsEnabled = !isMain,
            Foreground = accentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            Padding = new Thickness(2, 0, 2, 0),
            Style = checkboxStyle
        };
        Grid.SetColumn(checkbox, 1);
        row.Children.Add(checkbox);

        checkbox.Checked += (_, _) =>
        {
            SetCharacterVisibilitySafe(characterName, true);
        };

        checkbox.Unchecked += (_, _) =>
        {
            if (isMain)
            {
                checkbox.IsChecked = true;
                return;
            }

            SetCharacterVisibilitySafe(characterName, false);
        };

        var actionsPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(actionsPanel, 2);

        if (!isMain)
        {
            var removeButton = new System.Windows.Controls.Button
            {
                Content = "✕",
                ToolTip = "Retirer définitivement ce personnage",
                Foreground = new SolidColorBrush(borderColor),
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(0, 0, 4, 0),
                MinWidth = 24,
                MinHeight = 24
            };
            actionsPanel.Children.Add(removeButton);
            removeButton.Click += (_, _) =>
            {
                try
                {
                    if (TryRemoveCharacter(characterName))
                    {
                        UpdateCharactersList();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("SettingsWindow", $"Impossible de retirer {characterName}: {ex.Message}");
                }
            };
        }

        row.Children.Add(actionsPanel);
        border.Child = row;
        CharactersListContainer.Children.Add(border);
    }
    
    #endregion
    
    #region Chemins de logs
    
    private void BrowseKikimeterLogPath_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Fichier de log Wakfu (*.log)|*.log|Tous les fichiers (*.*)|*.*",
            Title = "Sélectionner le fichier wakfu.log",
            CheckFileExists = true
        };

        if (!string.IsNullOrEmpty(KikimeterLogPath) && File.Exists(KikimeterLogPath))
        {
            openFileDialog.InitialDirectory = Path.GetDirectoryName(KikimeterLogPath);
            openFileDialog.FileName = Path.GetFileName(KikimeterLogPath);
        }

        if (openFileDialog.ShowDialog() == true)
        {
            KikimeterLogPath = openFileDialog.FileName;
        }
    }
    
    private void AutoDetectSteamLogPath_Click(object sender, RoutedEventArgs e)
    {
        var foundFiles = WakfuLogFinder.FindAllLogFiles();
        
        // Chercher spécifiquement un chemin Steam
        var steamPath = foundFiles.FirstOrDefault(f => 
            f.Contains("Steam", StringComparison.OrdinalIgnoreCase) || 
            f.Contains("steamapps", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("SteamLibrary", StringComparison.OrdinalIgnoreCase));
        
        if (steamPath != null)
        {
            KikimeterLogPath = steamPath;
            MessageBox.Show(
                $"Fichier Steam détecté :\n{steamPath}",
                "Détection Steam",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                "Aucun fichier de log Wakfu n'a été trouvé dans les installations Steam.",
                "Fichier Steam introuvable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    
    private void AutoDetectAnkamaLogPath_Click(object sender, RoutedEventArgs e)
    {
        var foundFiles = WakfuLogFinder.FindAllLogFiles();
        
        // Chercher spécifiquement un chemin Ankama Launcher
        var ankamaPath = foundFiles.FirstOrDefault(f => 
            f.Contains("zaap", StringComparison.OrdinalIgnoreCase) || 
            f.Contains("Ankama", StringComparison.OrdinalIgnoreCase));
        
        if (ankamaPath != null)
        {
            KikimeterLogPath = ankamaPath;
            MessageBox.Show(
                $"Fichier Ankama Launcher détecté :\n{ankamaPath}",
                "Détection Ankama Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                "Aucun fichier de log Wakfu n'a été trouvé dans les installations Ankama Launcher.",
                "Fichier Ankama Launcher introuvable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    
    private void BrowseLootChatLogPath_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Fichier de log chat Wakfu (*.log)|*.log|Tous les fichiers (*.*)|*.*",
            Title = "Sélectionner le fichier wakfu_chat.log",
            CheckFileExists = true
        };

        if (!string.IsNullOrEmpty(LootChatLogPath) && File.Exists(LootChatLogPath))
        {
            openFileDialog.InitialDirectory = Path.GetDirectoryName(LootChatLogPath);
            openFileDialog.FileName = Path.GetFileName(LootChatLogPath);
        }

        if (openFileDialog.ShowDialog() == true)
        {
            LootChatLogPath = openFileDialog.FileName;
        }
    }
    
    private void AutoDetectSteamLootLogPath_Click(object sender, RoutedEventArgs e)
    {
        var foundFiles = WakfuLogFinder.FindAllLogFiles();
        
        // Chercher spécifiquement un chemin Steam
        var steamPath = foundFiles.FirstOrDefault(f => 
            f.Contains("Steam", StringComparison.OrdinalIgnoreCase) || 
            f.Contains("steamapps", StringComparison.OrdinalIgnoreCase) ||
            f.Contains("SteamLibrary", StringComparison.OrdinalIgnoreCase));
        
        if (steamPath != null)
        {
            // Chercher le fichier wakfu_chat.log correspondant
            var chatLogPath = WakfuLogFinder.FindChatLogFile(steamPath);
            if (!string.IsNullOrEmpty(chatLogPath))
            {
                LootChatLogPath = chatLogPath;
                MessageBox.Show(
                    $"Fichier Steam détecté :\n{chatLogPath}",
                    "Détection Steam",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Fichier wakfu.log Steam trouvé :\n{steamPath}\n\nMais le fichier wakfu_chat.log correspondant est introuvable.",
                    "Fichier chat introuvable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show(
                "Aucun fichier de log Wakfu n'a été trouvé dans les installations Steam.",
                "Fichier Steam introuvable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    
    private void AutoDetectAnkamaLootLogPath_Click(object sender, RoutedEventArgs e)
    {
        var foundFiles = WakfuLogFinder.FindAllLogFiles();
        
        // Chercher spécifiquement un chemin Ankama Launcher
        var ankamaPath = foundFiles.FirstOrDefault(f => 
            f.Contains("zaap", StringComparison.OrdinalIgnoreCase) || 
            f.Contains("Ankama", StringComparison.OrdinalIgnoreCase));
        
        if (ankamaPath != null)
        {
            // Chercher le fichier wakfu_chat.log correspondant
            var chatLogPath = WakfuLogFinder.FindChatLogFile(ankamaPath);
            if (!string.IsNullOrEmpty(chatLogPath))
            {
                LootChatLogPath = chatLogPath;
                MessageBox.Show(
                    $"Fichier Ankama Launcher détecté :\n{chatLogPath}",
                    "Détection Ankama Launcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Fichier wakfu.log Ankama Launcher trouvé :\n{ankamaPath}\n\nMais le fichier wakfu_chat.log correspondant est introuvable.",
                    "Fichier chat introuvable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show(
                "Aucun fichier de log Wakfu n'a été trouvé dans les installations Ankama Launcher.",
                "Fichier Ankama Launcher introuvable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    
    private void ResetLootButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("SettingsWindow", "Reset demandé depuis le bouton Reset");
            
            // 1. Réinitialiser le fichier de configuration des personnages (comme LootWindow_ResetButton_ExtraHandler)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var lootDir = Path.Combine(appData, "Amaliassistant", "Loot");
            Directory.CreateDirectory(lootDir);

            var configPath = Path.Combine(lootDir, "loot_characters.json");
            var freshConfig = new LootCharacterConfig
            {
                LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(freshConfig, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(configPath, json);
            Logger.Info("SettingsWindow", $"loot_characters.json réinitialisé ({configPath})");
            
            // 2. Réinitialiser la liste des personnages
            ResetCharacterList();
            
            // 3. Réinitialiser l'ordre des personnages
            ResetPlayerOrder();
            
            // 4. Appeler le callback si fourni (pour réinitialiser le Kikimeter)
            _onResetLootRequested?.Invoke();
            
            Logger.Info("SettingsWindow", "Reset complet effectué : loot, liste des personnages et ordre réinitialisés");
            
            MessageBox.Show(
                "Reset effectué :\n- Liste des loots réinitialisée\n- Liste des personnages réinitialisée\n- Ordre des personnages réinitialisé",
                "Reset effectué",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur lors du reset: {ex.Message}");
            MessageBox.Show(
                $"Erreur lors de la réinitialisation: {ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private void ConfirmLogPaths_Click(object sender, RoutedEventArgs e)
    {
        // Valider les chemins si remplis
        if (!string.IsNullOrWhiteSpace(KikimeterLogPath) && !File.Exists(KikimeterLogPath))
        {
            MessageBox.Show(
                "Le fichier de log Kikimeter spécifié n'existe pas.",
                "Fichier introuvable",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }
        
        if (!string.IsNullOrWhiteSpace(LootChatLogPath) && !File.Exists(LootChatLogPath))
        {
            MessageBox.Show(
                "Le fichier de log Loot spécifié n'existe pas.",
                "Fichier introuvable",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }
        
        // Sauvegarder la configuration
        _config.KikimeterLogPath = string.IsNullOrWhiteSpace(KikimeterLogPath) ? null : KikimeterLogPath;
        _config.LootChatLogPath = string.IsNullOrWhiteSpace(LootChatLogPath) ? null : LootChatLogPath;
        _onConfigChanged(_config);
        
        // Réinitialiser le détecteur de personnages si nécessaire
        CleanupCharacterDetector();
        TryInitializeCharacterDetector();
        UpdateCharactersList();
        
        MessageBox.Show(
            "Les chemins de logs ont été sauvegardés.",
            "Configuration sauvegardée",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    
    #endregion
    
    #region Démarrage automatique
    
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryValueName = "Amaliassistant";
    
    private bool IsStartupEnabled()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false))
            {
                if (key == null)
                    return false;
                
                var value = key.GetValue(StartupRegistryValueName);
                return value != null && !string.IsNullOrEmpty(value.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur lors de la vérification du démarrage automatique: {ex.Message}");
            return false;
        }
    }
    
    private void EnableStartup()
    {
        try
        {
            string? exePath = null;
            
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
            {
                exePath = assemblyLocation;
            }
            
            if (string.IsNullOrEmpty(exePath))
            {
                try
                {
                    var processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                    {
                        exePath = processPath;
                    }
                }
                catch { }
            }
            
            if (string.IsNullOrEmpty(exePath))
            {
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 0 && !string.IsNullOrEmpty(args[0]) && File.Exists(args[0]))
                {
                    exePath = args[0];
                }
            }
            
            if (string.IsNullOrEmpty(exePath))
            {
                Logger.Error("SettingsWindow", "Impossible de déterminer le chemin de l'exécutable");
                MessageBox.Show(
                    "Impossible de déterminer le chemin de l'application. Le démarrage automatique n'a pas pu être activé.",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            
            using (var key = Registry.CurrentUser.CreateSubKey(StartupRegistryKey, true))
            {
                if (key != null)
                {
                    key.SetValue(StartupRegistryValueName, $"\"{exePath}\"");
                    Logger.Info("SettingsWindow", $"Démarrage automatique activé: {exePath}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur lors de l'activation du démarrage automatique: {ex.Message}");
            MessageBox.Show(
                $"Erreur lors de l'activation du démarrage automatique: {ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private void DisableStartup()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
            {
                if (key != null)
                {
                    key.DeleteValue(StartupRegistryValueName, false);
                    Logger.Info("SettingsWindow", "Démarrage automatique désactivé");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"Erreur lors de la désactivation du démarrage automatique: {ex.Message}");
            MessageBox.Show(
                $"Erreur lors de la désactivation du démarrage automatique: {ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private void StartWithWindowsCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        EnableStartup();
        _config.StartWithWindows = true;
        _onConfigChanged(_config);
    }
    
    private void StartWithWindowsCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        DisableStartup();
        _config.StartWithWindows = false;
        _onConfigChanged(_config);
    }
    
    #endregion

    #region Character helpers

    private LootCharacterConfig GetActiveCharacterConfig()
    {
        if (_characterDetector != null)
        {
            try
            {
                return _characterDetector.GetConfig();
            }
            catch (Exception ex)
            {
                Logger.Warning("SettingsWindow", $"GetActiveCharacterConfig (detector) : {ex.Message}");
            }
        }

        return LoadCharacterConfigFromDisk();
    }

    private LootCharacterConfig LoadCharacterConfigFromDisk()
    {
        try
        {
            Directory.CreateDirectory(LootConfigDirectory);
            var path = GetLootConfigPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<LootCharacterConfig>(json);
                if (config != null)
                {
                    config.Characters = new Dictionary<string, bool>(config.Characters ?? new Dictionary<string, bool>(), StringComparer.OrdinalIgnoreCase);
                    config.MyCharacters ??= new List<string>();
                    config.ManualCharacters ??= new List<string>();
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("SettingsWindow", $"LoadCharacterConfigFromDisk: {ex.Message}");
        }

        return new LootCharacterConfig();
    }

    private void SaveCharacterConfigToDisk(LootCharacterConfig config)
    {
        try
        {
            Directory.CreateDirectory(LootConfigDirectory);
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(GetLootConfigPath(), json);
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"SaveCharacterConfigToDisk: {ex.Message}");
        }
    }

    private void ResetCharacterConfigFile()
    {
        var emptyConfig = new LootCharacterConfig
        {
            LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        SaveCharacterConfigToDisk(emptyConfig);
    }

    private void SetCharacterVisibilitySafe(string characterName, bool isVisible)
    {
        if (_characterDetector != null)
        {
            _characterDetector.SetCharacterVisibility(characterName, isVisible);
            return;
        }

        try
        {
            var config = LoadCharacterConfigFromDisk();
            if (string.Equals(characterName, config.MainCharacter, StringComparison.OrdinalIgnoreCase) && !isVisible)
            {
                Logger.Info("SettingsWindow", $"Tentative d'invisibiliser le personnage principal {characterName} ignorée");
                return;
            }

            if (config.Characters.ContainsKey(characterName) || isVisible)
            {
                config.Characters[characterName] = isVisible;
                SaveCharacterConfigToDisk(config);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"SetCharacterVisibilitySafe: {ex.Message}");
        }
    }

    private bool TrySetMainCharacter(string characterName)
    {
        if (_characterDetector != null)
        {
            _characterDetector.SetMainCharacter(characterName);
            return true;
        }

        try
        {
            var config = LoadCharacterConfigFromDisk();
            var normalized = characterName?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            config.Characters[normalized] = true;
            config.MainCharacter = normalized;

            config.MyCharacters ??= new List<string>();
            config.ManualCharacters ??= new List<string>();

            config.MyCharacters.RemoveAll(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase));
            config.MyCharacters.Insert(0, normalized);
            if (config.MyCharacters.Count > 3)
            {
                config.MyCharacters = config.MyCharacters.Take(3).ToList();
            }

            SaveCharacterConfigToDisk(config);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"TrySetMainCharacter: {ex.Message}");
            return false;
        }
    }

    private bool TryRemoveCharacter(string characterName)
    {
        if (_characterDetector != null)
        {
            _characterDetector.RemoveCharacter(characterName);
            return true;
        }

        try
        {
            var config = LoadCharacterConfigFromDisk();
            if (string.Equals(characterName, config.MainCharacter, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning("SettingsWindow", $"Impossible de retirer le personnage principal {characterName}");
                return false;
            }

            bool changed = false;
            if (config.Characters.Remove(characterName))
            {
                changed = true;
            }

            if (config.ManualCharacters?.RemoveAll(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                changed = true;
            }

            if (config.MyCharacters?.RemoveAll(c => string.Equals(c, characterName, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                changed = true;
            }

            if (changed)
            {
                SaveCharacterConfigToDisk(config);
            }

            return changed;
        }
        catch (Exception ex)
        {
            Logger.Error("SettingsWindow", $"TryRemoveCharacter: {ex.Message}");
            return false;
        }
    }

    private List<string> LoadOrderedNamesFromManualOrder()
    {
        var orderedNames = new List<string>();
        try
        {
            var state = PersistentStorageHelper.LoadJsonWithFallback<ManualOrderState>(ManualOrderConfigFileName);
            if (state?.BaselineRoster != null && state.BaselineRoster.Count > 0)
            {
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                orderedNames = state.BaselineRoster
                    .Where(name => !string.IsNullOrWhiteSpace(name) && unique.Add(name))
                    .ToList();
            }
            else if (state?.Orders != null && state.Orders.Count > 0)
            {
                orderedNames = state.Orders
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("SettingsWindow", $"LoadOrderedNamesFromManualOrder: {ex.Message}");
        }

        return orderedNames;
    }

    private Dictionary<string, int> LoadManualOrderMap()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var state = PersistentStorageHelper.LoadJsonWithFallback<ManualOrderState>(ManualOrderConfigFileName);
            if (state?.Orders != null)
            {
                foreach (var kvp in state.Orders)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        map[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("SettingsWindow", $"LoadManualOrderMap: {ex.Message}");
        }

        return map;
    }

    private List<string> LoadCharacterNamesFromConfig()
    {
        try
        {
            var config = LoadCharacterConfigFromDisk();
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string? name)
            {
                if (string.IsNullOrWhiteSpace(name) || seen.Contains(name))
                {
                    return;
                }

                seen.Add(name);
                ordered.Add(name);
            }

            Add(config.MainCharacter);

            if (config.MyCharacters != null)
            {
                foreach (var name in config.MyCharacters)
                {
                    Add(name);
                }
            }

            if (config.ManualCharacters != null)
            {
                foreach (var name in config.ManualCharacters)
                {
                    Add(name);
                }
            }

            foreach (var name in config.Characters.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                Add(name);
            }

            return ordered;
        }
        catch (Exception ex)
        {
            Logger.Warning("SettingsWindow", $"LoadCharacterNamesFromConfig: {ex.Message}");
            return new List<string>();
        }
    }

    private static string GetLootConfigPath() => Path.Combine(LootConfigDirectory, LootCharactersFileName);

    private sealed class ManualOrderState
    {
        public bool UseManualOrder { get; set; }
        public Dictionary<string, int>? Orders { get; set; }
        public List<string>? BaselineRoster { get; set; }
    }

    #endregion
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
