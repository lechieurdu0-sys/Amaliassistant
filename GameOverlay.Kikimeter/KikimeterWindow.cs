using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Kikimeter.Services;
using GameOverlay.Kikimeter.Views;
using GameOverlay.Models;
using GameOverlay.Themes;
using GameOverlay.XpTracker.Models;
using Newtonsoft.Json;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace GameOverlay.Kikimeter;

public partial class KikimeterWindow : Window, INotifyPropertyChanged
{
    private string _logPath;
    private KikimeterIndividualMode _individualMode;
    private Services.LogFileWatcher? _logWatcher;
    private DispatcherTimer? _updateTimer;
    private ObservableCollection<PlayerStats> _playersCollection = new();
    private PlayerStats _previewPlayer = new PlayerStats { Name = "En attente..." };
    private Dictionary<string, int> _playerKikis = new();
    private Dictionary<string, Views.PlayerWindow> _playerWindows = new();
    private readonly Dictionary<string, XpProgressWindow> _xpWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, XpGainEvent> _lastXpEvents = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, XpWindowState> _xpWindowStates = new(StringComparer.OrdinalIgnoreCase);
    private IndicatorWindow? _indicatorWindow;
    private XpTrackingCoordinator? _xpCoordinator;
    private string _sectionBackgroundColor = "#FF232323";
    private bool _playerFramesOpaque;
    private bool _isLoadingPlayerFramesOpaque;
    private bool _isIndividualMode = false;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint = new();
    private long _displayedMaxDamage = 10000;
    private long _displayedMaxDamageTaken = 10000;
    private long _displayedMaxHealing = 10000;
    private long _displayedMaxShield = 10000;
    private long _maxDamage = 10000;
    private long _maxDamageTaken = 10000;
    private long _maxHealing = 10000;
    private long _maxShield = 10000;
    private bool _isHorizontalMode;
    private System.Windows.Size _storedVerticalSize = System.Windows.Size.Empty;
    private System.Windows.Size _storedHorizontalSize = System.Windows.Size.Empty;
    private const string HorizontalModeConfigFileName = "kikimeter_horizontal_mode.json";
    private bool _suspendWindowPersistence;
    private bool _userRequestedHidden;
    private const string XpWindowStateFileName = "xp_windows.json";
    private const string ManualOrderConfigFileName = "kikimeter_manual_order.json";
    private const double XpWindowDefaultWidth = 340d;
    private const double XpWindowDefaultHeight = 62d;
    private Dictionary<string, int> _manualOrderMap = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _manualOrderBaseline = new();
    private bool _useManualOrder;
    private bool _suspendFocusReturn;
    private bool _isCollapsed = false;
    private DateTime _lastClickTime = DateTime.MinValue;
    private bool _firstTurnComplete = false; // Indique si le premier tour complet est terminé
    private Services.PlayerManagementService? _playerManagementService;
    private Services.IPlayerDataProvider? _playerDataProvider;
    private DateTime _lastPlayerSyncTime = DateTime.MinValue;
    private const int PlayerSyncIntervalSeconds = 5; // Synchronisation toutes les 5 secondes
    
    // Flags de protection pour l'initialisation et le reset
    private bool _isInitialized = false;
    private bool _isResetInProgress = false;
    private bool _isInitializing = false;
    
    // Flag pour figer le Kikimeter après le combat (pour analyse des stats)
    // true → le Kikimeter est figé et ne se synchronise plus avec JSON/LogParser
    // false → le Kikimeter fonctionne normalement
    // IMPORTANT: Ce flag ne doit PAS bloquer l'initialisation du prochain combat
    private bool _freezeAfterCombat = false;
    
    // Flag pour signaler qu'un nouveau combat vient de commencer
    // true → on vient de débuter un nouveau combat, nécessite initialisation complète des joueurs
    // false → mise à jour normale en diff
    private bool _isNewCombat = false;

    private static bool _bindingDiagnosticsEnabled;
    private static readonly object _bindingDiagnosticsLock = new();

    public KikimeterWindow(string logPath, KikimeterIndividualMode individualMode)
    {
        EnsureBindingDiagnostics();
        _logPath = logPath;
        _individualMode = individualMode;
        _isIndividualMode = individualMode.IndividualMode;
        InitializeComponent();
        _storedVerticalSize = new System.Windows.Size(Width, Height);
        SizeChanged += KikimeterWindow_SizeChanged;
        UpdateAccentBrushResource();
        ThemeManager.AccentColorChanged += (s, args) =>
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateAccentBrushResource()));
        };
        DataContext = this;
        _playersCollection = new ObservableCollection<PlayerStats>();
        if (PlayersItemsControl != null)
        {
            PlayersItemsControl.ItemsSource = _playersCollection;
            
            // Aperçu de design : ajouter des joueurs d'exemple pour voir le mode "combat"
            // Les deux modes (en attente + combat) seront visibles en même temps dans le designer
            #if DEBUG
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                // Ajouter 3 joueurs fictifs pour l'aperçu du mode "combat"
                var designPlayer1 = new PlayerStats { Name = "JoueurExemple1", DamageDealt = 7500, DamageTaken = 2500, HealingDone = 1200, ShieldGiven = 800, DamageThisTurn = 1500, ClassName = "Cra" };
                var designPlayer2 = new PlayerStats { Name = "JoueurExemple2", DamageDealt = 5200, DamageTaken = 1800, HealingDone = 2000, ShieldGiven = 600, DamageThisTurn = 1300, ClassName = "Eniripsa" };
                var designPlayer3 = new PlayerStats { Name = "JoueurExemple3", DamageDealt = 6800, DamageTaken = 3200, HealingDone = 800, ShieldGiven = 400, DamageThisTurn = 1400, ClassName = "Iop" };
                _playersCollection.Add(designPlayer1);
                _playersCollection.Add(designPlayer2);
                _playersCollection.Add(designPlayer3);
            }
            #endif
        }
        
        // Initialiser le ContentControl de l'aperçu avec le PlayerStats "En attente..."
        var previewBorder = this.FindName("PreviewPlayerBorder") as System.Windows.Controls.ContentControl;
        if (previewBorder != null)
        {
            _previewPlayer.IsFirst = true; // Le joueur en attente est toujours le premier
            previewBorder.Content = _previewPlayer;
            
            // Aperçu de design : afficher le mode "en attente" ET le mode "combat" en même temps
            #if DEBUG
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                // En mode design, forcer la visibilité du mode "en attente" même s'il y a des joueurs
                previewBorder.Visibility = Visibility.Visible;
            }
            #endif
        }
        
        // L'initialisation des flèches se fera dans KikimeterWindow_Loaded
        // où tous les éléments sont garantis d'être initialisés (identique Debug/Release)
        
        // S'abonner aux changements de la collection pour afficher/masquer l'aperçu
        _playersCollection.CollectionChanged += (s, e) =>
        {
            UpdatePreviewVisibility();
            UpdateFirstPlayerFlag();
            // Mettre à jour les flèches quand on passe du mode "en attente" au mode "combat" ou vice versa
            UpdateMainArrowRotation();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateWindowHeight();
            }), DispatcherPriority.Loaded);
        };
        LoadIndividualMode();
        LoadHorizontalModeState();
        LoadManualOrderState();
        OnPropertyChanged(nameof(IsHorizontalMode));
        OnPropertyChanged(nameof(PlayerModuleWidth));
        OnPropertyChanged(nameof(PlayerModuleHorizontalAlignment));
        UpdateLayoutMode();
        UpdateFirstPlayerFlag();
        UpdateTemplate();
        LoadXpWindowStates();
        InitializeWindow();
        StartWatching();
        SourceInitialized += KikimeterWindow_SourceInitialized;
        Loaded += KikimeterWindow_Loaded;
        Loaded += (s, e) => 
        {
            LoadSectionBackgroundColor();
            UpdatePreviewVisibility();
            UpdateWindowHeight();
            RestoreXpWindows();
            ApplyManualOrderIfNeeded();
        };
        Logger.Info("KikimeterWindow", "KikimeterWindow créé avec timer de 50ms");
    }
    
    public ObservableCollection<PlayerStats> PlayersCollection => _playersCollection;
    
    /// <summary>
    /// Obtient le service de gestion des joueurs (SOURCE UNIQUE DE VÉRITÉ)
    /// </summary>
    public Services.PlayerManagementService? PlayerManagementService => _playerManagementService;
    
    /// <summary>
    /// Applique l'ordre des joueurs depuis SettingsWindow
    /// </summary>
    public void SetPlayerOrder(IReadOnlyList<string> orderedNames)
    {
        if (orderedNames == null || orderedNames.Count == 0)
        {
            _useManualOrder = false;
            _manualOrderMap.Clear();
            _manualOrderBaseline.Clear();
            SaveManualOrderState();
            return;
        }
        
        try
        {
            // Mettre à jour le dictionnaire d'ordre manuel
            for (int i = 0; i < orderedNames.Count; i++)
            {
                _manualOrderMap[orderedNames[i]] = i;
            }

            SetManualOrderBaseline(orderedNames);
            
            // Appliquer l'ordre aux joueurs existants
            foreach (var player in _playersCollection)
            {
                if (_manualOrderMap.TryGetValue(player.Name, out var order))
                {
                    player.ManualOrder = order;
                }
            }
            
            _useManualOrder = true;
            
            // Trier les joueurs selon l'ordre défini (si la roster correspond)
            ApplyManualOrderIfNeeded();
            SaveManualOrderState();
            
            Logger.Info("KikimeterWindow", $"Ordre des joueurs appliqué depuis SettingsWindow: {string.Join(", ", orderedNames)}");
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors de l'application de l'ordre des joueurs: {ex.Message}");
        }
    }
    
    private void UpdatePreviewVisibility()
    {
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var previewBorder = this.FindName("PreviewPlayerBorder") as System.Windows.Controls.ContentControl;
                if (previewBorder != null)
                {
                    #if DEBUG
                    // En mode design, toujours afficher l'aperçu pour voir les deux modes en même temps
                    bool isDesignMode = System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject());
                    if (isDesignMode)
                    {
                        previewBorder.Visibility = Visibility.Visible;
                        _previewPlayer.IsFirst = true;
                    }
                    else
                    #endif
                    {
                        // En mode runtime, afficher l'aperçu seulement s'il n'y a pas de joueurs
                    previewBorder.Visibility = _playersCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        // S'assurer que IsFirst est toujours true pour le preview player
                        if (_playersCollection.Count == 0)
                        {
                            _previewPlayer.IsFirst = true;
                        }
                    }
                }
            }));
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdatePreviewVisibility: {ex.Message}");
        }
    }
    
    private void UpdateTemplate()
    {
        try
        {
            if (PlayersItemsControl != null)
            {
                var resources = this.Resources;
                if (_isCollapsed)
                {
                    var collapsedTemplate = resources["PlayerStatsCollapsedTemplate"] as System.Windows.DataTemplate;
                    if (collapsedTemplate != null)
                    {
                        PlayersItemsControl.ItemTemplate = collapsedTemplate;
                    }
                }
                else
                {
                    var normalTemplate = resources["PlayerStatsDataTemplate"] as System.Windows.DataTemplate;
                    if (normalTemplate != null)
                    {
                        PlayersItemsControl.ItemTemplate = normalTemplate;
                    }
                }
                
                // Attacher les gestionnaires d'événements aux flèches après le chargement
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AttachArrowEventHandlers();
                    UpdateStatsVisibility();
                }), DispatcherPriority.Loaded);
                
                UpdateFirstPlayerFlag();
                UpdateWindowHeight();
                
                // Ne pas mettre à jour la position de la flèche lors du changement de template
                // La flèche reste au même endroit, seule la visibilité change
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdateTemplate: {ex.Message}");
        }
    }
    
    private void AttachArrowEventHandlers()
    {
        try
        {
            if (PlayersItemsControl == null) return;
            
            foreach (var item in PlayersItemsControl.Items)
            {
                var container = PlayersItemsControl.ItemContainerGenerator.ContainerFromItem(item);
                if (container != null)
                {
                    // Utiliser VisualTreeHelper pour trouver les éléments
                    var collapseArrow = FindVisualChild<System.Windows.Controls.Image>(container, "CollapseArrow");
                    if (collapseArrow != null)
                    {
                        collapseArrow.MouseLeftButtonDown -= CollapseArrow_Click;
                        collapseArrow.MouseLeftButtonDown += CollapseArrow_Click;
                    }
                    
                    var deployArrow = FindVisualChild<System.Windows.Controls.Image>(container, "DeployArrow");
                    if (deployArrow != null)
                    {
                        deployArrow.MouseLeftButtonDown -= DeployArrow_Click;
                        deployArrow.MouseLeftButtonDown += DeployArrow_Click;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans AttachArrowEventHandlers: {ex.Message}");
        }
    }
    
    private static T? FindVisualChild<T>(System.Windows.DependencyObject parent, string name) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t && (child as System.Windows.FrameworkElement)?.Name == name)
            {
                return t;
            }
            
            var childOfChild = FindVisualChild<T>(child, name);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }
    
    private void CollapseArrow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isCollapsed = true;
        OnPropertyChanged(nameof(IsCollapsed));
        UpdateTemplate();
        UpdateWindowHeight();
        ForceArrowVisibility();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateStatsVisibility();
        }), DispatcherPriority.Loaded);
        e.Handled = true;
    }
    
    private void DeployArrow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isCollapsed = false;
        OnPropertyChanged(nameof(IsCollapsed));
        UpdateTemplate();
        UpdateWindowHeight();
        ForceArrowVisibility();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateStatsVisibility();
        }), DispatcherPriority.Loaded);
        e.Handled = true;
    }
    
    private void MainCollapseArrow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            // Empêcher les clics multiples rapides
            if (DateTime.Now.Subtract(_lastClickTime).TotalMilliseconds < 500)
            {
                e.Handled = true;
                return;
            }
            
            _lastClickTime = DateTime.Now;
            
            Logger.Info("KikimeterWindow", $"MainCollapseArrow_Click appelé! _isCollapsed avant: {_isCollapsed}");
            
            // Changer l'état
            _isCollapsed = !_isCollapsed;
            OnPropertyChanged(nameof(IsCollapsed));
            
            Logger.Info("KikimeterWindow", $"IsCollapsed après: {_isCollapsed}");
            
            // Mettre à jour le template et la hauteur
            UpdateTemplate();
            UpdateWindowHeight();
            
            // Forcer la visibilité - logique spécifique pour Release
            ForceArrowVisibility();
            
            // Forcer la mise à jour de la visibilité des stats pour tous les items
            // Utiliser plusieurs tentatives pour s'assurer que les containers sont générés
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateStatsVisibility();
                // Réessayer après un court délai pour s'assurer que tout est généré
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateStatsVisibility();
                }), DispatcherPriority.Render);
            }), DispatcherPriority.Loaded);
            
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"CollapseArrow error: {ex.Message}\n{ex.StackTrace}");
            e.Handled = true;
        }
    }
    
    private void ForceArrowVisibility()
    {
        // Méthode dédiée pour forcer la visibilité des flèches
        // Les flèches sont maintenant dans des Border pour faciliter le positionnement
        var expandedBorder = this.FindName("MainCollapseArrowExpandedBorderInline") as System.Windows.Controls.Border;
        var collapsedBorder = this.FindName("MainCollapseArrowCollapsedBorderInline") as System.Windows.Controls.Border;
        
        if (expandedBorder == null)
        {
            Logger.Warning("KikimeterWindow", "MainCollapseArrowExpandedBorderInline est null!");
        }
        
        if (collapsedBorder == null)
        {
            Logger.Warning("KikimeterWindow", "MainCollapseArrowCollapsedBorderInline est null!");
        }
        
        if (expandedBorder != null && collapsedBorder != null)
        {
            // Les deux modes utilisent maintenant les mêmes flèches
            if (_isCollapsed)
            {
                expandedBorder.Visibility = Visibility.Collapsed;
                collapsedBorder.Visibility = Visibility.Visible;
                Logger.Info("KikimeterWindow", $"Flèches mises à jour: collapsed visible (état: collapsed)");
            }
            else
            {
                expandedBorder.Visibility = Visibility.Visible;
                collapsedBorder.Visibility = Visibility.Collapsed;
                Logger.Info("KikimeterWindow", $"Flèches mises à jour: expanded visible (état: expanded)");
            }
            
            // Forcer le rendu de manière très agressive
            expandedBorder.InvalidateVisual();
            collapsedBorder.InvalidateVisual();
            expandedBorder.UpdateLayout();
            collapsedBorder.UpdateLayout();
            
            // Forcer le rendu du Grid parent
            var gridParent = expandedBorder.Parent as System.Windows.Controls.Grid;
            if (gridParent != null)
            {
                gridParent.InvalidateVisual();
                gridParent.UpdateLayout();
            }
            
            // Forcer la mise à jour de la fenêtre
            this.InvalidateVisual();
            this.UpdateLayout();
        }
        else
        {
            Logger.Error("KikimeterWindow", $"Impossible de trouver les flèches! Expanded: {expandedBorder != null}, Collapsed: {collapsedBorder != null}");
        }
        
        // Forcer la mise à jour du layout de la fenêtre
        this.UpdateLayout();
        CommandManager.InvalidateRequerySuggested();
    }
    
    private void UpdateVisualState()
    {
        // Forcer la mise à jour des éléments visuels
        var expandedBorder = this.FindName("MainCollapseArrowExpandedBorderInline") as System.Windows.Controls.Border;
        var collapsedBorder = this.FindName("MainCollapseArrowCollapsedBorderInline") as System.Windows.Controls.Border;
        
        if (expandedBorder != null)
        {
            expandedBorder.InvalidateVisual();
            expandedBorder.UpdateLayout();
        }
        if (collapsedBorder != null)
        {
            collapsedBorder.InvalidateVisual();
            collapsedBorder.UpdateLayout();
        }
    }
    
    // Les événements Loaded ne sont plus nécessaires car les DataTriggers gèrent la visibilité
    
    private void UpdateMainArrowRotation()
    {
        try
        {
            Logger.Info("KikimeterWindow", $"UpdateMainArrowRotation - _isCollapsed: {_isCollapsed}, Players: {_playersCollection.Count}");
            
            // Utiliser la méthode dédiée qui gère aussi le cas Release et les modes combat/en attente
            ForceArrowVisibility();
            
            // Forcer la mise à jour de la visibilité des stats
            UpdateStatsVisibility();
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdateMainArrowRotation: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private void UpdateStatsVisibility()
    {
        try
        {
            if (PlayersItemsControl == null) return;
            
            // Forcer la génération des containers si nécessaire
            PlayersItemsControl.UpdateLayout();
            
            foreach (var item in PlayersItemsControl.Items)
            {
                var container = PlayersItemsControl.ItemContainerGenerator.ContainerFromItem(item);
                if (container == null)
                {
                    // Si le container n'est pas encore généré, forcer la génération
                    PlayersItemsControl.ItemContainerGenerator.StatusChanged += (s, e) =>
                    {
                        if (PlayersItemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                        {
                            UpdateStatsVisibility();
                        }
                    };
                    continue;
                }
                
                // Chercher le Grid StatsGrid dans le container
                var statsGrid = FindVisualChild<System.Windows.Controls.Grid>(container, "StatsGrid");
                if (statsGrid != null)
                {
                    Logger.Debug("KikimeterWindow", $"StatsGrid trouvé pour {item}, _isCollapsed={_isCollapsed}");
                    statsGrid.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
                    if (_isCollapsed)
                    {
                        statsGrid.Height = 0;
                        statsGrid.Margin = new Thickness(0, 0, 0, 0);
                        statsGrid.Opacity = 0;
                        Logger.Debug("KikimeterWindow", $"StatsGrid masqué pour {item}");
                    }
                    else
                    {
                        statsGrid.Height = 95;
                        statsGrid.Margin = new Thickness(0, -5, 0, 0);
                        statsGrid.Opacity = 1;
                        Logger.Debug("KikimeterWindow", $"StatsGrid affiché pour {item}");
                    }
                    statsGrid.InvalidateVisual();
                    statsGrid.UpdateLayout();
                }
                else
                {
                    Logger.Warning("KikimeterWindow", $"StatsGrid non trouvé pour {item} - recherche dans tout l'arbre visuel");
                    // Essayer de trouver tous les Grids pour debug
                    var allGrids = FindAllVisualChildren<System.Windows.Controls.Grid>(container);
                    Logger.Info("KikimeterWindow", $"Grids trouvés dans le container: {allGrids.Count}");
                    foreach (var grid in allGrids)
                    {
                        var name = (grid as System.Windows.FrameworkElement)?.Name ?? "sans nom";
                        Logger.Info("KikimeterWindow", $"  - Grid trouvé: {name}");
                    }
                }
            }
            
            Logger.Info("KikimeterWindow", $"UpdateStatsVisibility terminé pour {PlayersItemsControl.Items.Count} items");
            
            // Forcer la mise à jour du layout de l'ItemsControl
            PlayersItemsControl.InvalidateVisual();
            PlayersItemsControl.UpdateLayout();
            this.InvalidateVisual();
            this.UpdateLayout();
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdateStatsVisibility: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private static List<T> FindAllVisualChildren<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        var children = new List<T>();
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                children.Add(t);
            }
            children.AddRange(FindAllVisualChildren<T>(child));
        }
        return children;
    }
    
    private void UpdateFirstPlayerFlag()
    {
        try
        {
            for (int i = 0; i < _playersCollection.Count; i++)
            {
                _playersCollection[i].IsFirst = (i == 0);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdateFirstPlayerFlag: {ex.Message}");
        }
    }
    
    private void UpdateWindowHeight()
    {
        try
        {
            // Hauteur de la barre de titre
            const double titleBarHeight = 25;
            
            // Hauteur réelle d'un module de joueur selon l'état
            double playerModuleHeight;
            double spacingBetweenModules;
            
            if (_isCollapsed)
            {
                // Mode réduit : seulement breed_nom_dpt (~30px) + croix (25px) = ~55px
                playerModuleHeight = 55;
                spacingBetweenModules = 4; // 4px d'écart en mode réduit
            }
            else
            {
                // Mode déployé : Croix (25px) + breed_nom_dpt (~30px) + Canvas kikimeter (95px) + marges = ~150px
                playerModuleHeight = 150;
                spacingBetweenModules = 0; // Quasiment collé en mode déployé
            }
            
            // Nombre de modules à afficher
            int playerCount = _playersCollection.Count;
            if (playerCount == 0)
            {
                // En attente de combat : 1 module de prévisualisation
                playerCount = 1;
            }
            
            // NE PAS limiter - afficher tous les joueurs
            int displayCount = playerCount;
            
            // Calcul de la hauteur totale
            double totalHeight = titleBarHeight + (displayCount * playerModuleHeight) + ((displayCount - 1) * spacingBetweenModules);
            
            // S'assurer que la hauteur est suffisante
            if (totalHeight < MinHeight)
            {
                totalHeight = MinHeight;
            }
            
            // Ajuster la hauteur de la fenêtre et MaxHeight
            MaxHeight = double.PositiveInfinity; // Permettre toutes les hauteurs nécessaires
            if (Math.Abs(Height - totalHeight) > 1) // Tolérance de 1px pour éviter les recalculs infinis
            {
                Height = totalHeight;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdateWindowHeight: {ex.Message}");
        }
    }
    
    // La flèche collapse est maintenant positionnée à côté de la croix de déplacement
    // Plus besoin de calcul dynamique complexe - position fixe simple
    
    private void MainBorder_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        CreateContextMenu().IsOpen = true;
    }
    
    private System.Windows.Controls.ContextMenu CreateContextMenu()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu();
        
        // Menu pour changer la couleur de fond des joueurs
        var backgroundColorMenuItem = new System.Windows.Controls.MenuItem { Header = "🎨 Couleur de fond des joueurs" };
        
        // Couleurs prédéfinies
        var backgroundColors = new[]
        {
            ("Noir (défaut)", "#FF000000"),
            ("Gris très foncé", "#FF0F0F0F"),
            ("Gris foncé", "#FF1A1A1A"),
            ("Gris moyen", "#FF2A2A2A"),
            ("Gris clair", "#FF404040"),
            ("Bleu foncé", "#FF1A2A4A"),
            ("Violet foncé", "#FF2A1A4A"),
            ("Vert foncé", "#FF1A4A2A"),
            ("Rouge foncé", "#FF4A1A1A"),
            ("Marron foncé", "#FF3A2A1A"),
            ("Cyan foncé", "#FF1A3A4A")
        };
        
        foreach (var (name, hex) in backgroundColors)
        {
            var colorItem = new System.Windows.Controls.MenuItem { Header = name };
            
            // Afficher un carré de couleur
            var colorBox = new System.Windows.Controls.Border
            {
                Width = 16,
                Height = 16,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new System.Windows.Thickness(1),
                Margin = new System.Windows.Thickness(0, 0, 8, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            
            var stackPanel = new System.Windows.Controls.StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            stackPanel.Children.Add(colorBox);
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = name, VerticalAlignment = System.Windows.VerticalAlignment.Center });
            colorItem.Header = stackPanel;
            
            colorItem.Click += (s, e) =>
            {
                ApplyKikimeterSectionBackgroundColor(hex);
                SaveKikimeterSectionBackgroundColor(hex);
            };
            
            backgroundColorMenuItem.Items.Add(colorItem);
        }
        
        backgroundColorMenuItem.Items.Add(new System.Windows.Controls.Separator());
        
        // Sélecteur de couleur personnalisé
        var customColorItem = new System.Windows.Controls.MenuItem { Header = "🎨 Couleur personnalisée..." };
        customColorItem.Click += (s, e) => OpenKikimeterSectionColorPicker();
        backgroundColorMenuItem.Items.Add(customColorItem);
        
        // Réinitialiser au noir par défaut
        var resetColorItem = new System.Windows.Controls.MenuItem { Header = "🔄 Réinitialiser au noir par défaut" };
        resetColorItem.Click += (s, e) =>
        {
            ApplyKikimeterSectionBackgroundColor("#FF000000");
            SaveKikimeterSectionBackgroundColor("#FF000000");
        };
        backgroundColorMenuItem.Items.Add(resetColorItem);
        
        contextMenu.Items.Add(backgroundColorMenuItem);
        
        contextMenu.Items.Add(new System.Windows.Controls.Separator());
        
        var savePositionItem = new System.Windows.Controls.MenuItem { Header = "💾 Sauvegarder la position actuelle" };
        savePositionItem.Click += (s, e) =>
        {
            try
            {
                SaveWindowPositions();
                Logger.Info("KikimeterWindow", $"Position sauvegardée ({(_isHorizontalMode ? "horizontal" : "vertical")}) : X={Left:F0}, Y={Top:F0}, W={Width:F0}, H={Height:F0}");
            }
            catch (Exception ex)
            {
                Logger.Warning("KikimeterWindow", $"Impossible de sauvegarder la position: {ex.Message}");
            }
        };
        contextMenu.Items.Add(savePositionItem);
        
        // Synchroniser avec le thème
        SyncKikimeterWindowContextMenuTheme(contextMenu);
        
        // S'abonner aux changements de thème
        ThemeManager.AccentColorChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(new Action(() => SyncKikimeterWindowContextMenuTheme(contextMenu)));
        };
        ThemeManager.BubbleBackgroundColorChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(new Action(() => SyncKikimeterWindowContextMenuTheme(contextMenu)));
        };
        
        return contextMenu;
    }
    
    private void SyncKikimeterWindowContextMenuTheme(System.Windows.Controls.ContextMenu contextMenu)
    {
        try
        {
            ThemeManager.ApplyContextMenuTheme(contextMenu);
        }
        catch { }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateLayoutMode()
    {
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // ScrollViewer retiré - plus nécessaire

                if (PlayersItemsControl != null)
                {
                    ItemsPanelTemplate template = CreateItemsPanelTemplate(_isHorizontalMode);
                    PlayersItemsControl.ItemsPanel = template;
                    PlayersItemsControl.Items.Refresh();
                    PlayersItemsControl.UpdateLayout();
                }
                ApplyStoredWindowSize();
            }), DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"UpdateLayoutMode error: {ex.Message}");
        }
    }

    private static ItemsPanelTemplate CreateItemsPanelTemplate(bool horizontal)
    {
        FrameworkElementFactory factory;
        if (horizontal)
        {
            factory = new FrameworkElementFactory(typeof(WrapPanel));
            factory.SetValue(WrapPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
        }
        else
        {
            factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Vertical);
        }
        factory.SetValue(System.Windows.Controls.Panel.IsItemsHostProperty, true);
        return new ItemsPanelTemplate(factory);
    }

    private void LoadHorizontalModeState()
    {
        try
        {
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant");
            string configPath = Path.Combine(configDir, HorizontalModeConfigFileName);
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var data = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
                if (data != null && data.TryGetValue("HorizontalMode", out bool mode))
                {
                    _isHorizontalMode = mode;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"LoadHorizontalModeState error: {ex.Message}");
        }
    }

    private void SaveHorizontalModeState()
    {
        try
        {
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            string configPath = Path.Combine(configDir, HorizontalModeConfigFileName);
            var data = new Dictionary<string, bool>
            {
                ["HorizontalMode"] = _isHorizontalMode
            };
            File.WriteAllText(configPath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"SaveHorizontalModeState error: {ex.Message}");
        }
    }
    
    private void LoadManualOrderState()
    {
        try
        {
            var state = PersistentStorageHelper.LoadJsonWithFallback<ManualOrderState>(ManualOrderConfigFileName);
            if (state != null)
            {
                if (state.Orders != null)
                {
                    _manualOrderMap = new Dictionary<string, int>(state.Orders, StringComparer.OrdinalIgnoreCase);
                }
                _useManualOrder = _manualOrderMap.Count > 0 && state.UseManualOrder;
                if (state.BaselineRoster != null)
                {
                    SetManualOrderBaseline(state.BaselineRoster);
                }
                else
                {
                    _manualOrderBaseline = new List<string>();
                }
            }
            else
            {
                _manualOrderBaseline = new List<string>();
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"Impossible de charger l'ordre manuel: {ex.Message}");
            _manualOrderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _useManualOrder = false;
            _manualOrderBaseline = new List<string>();
        }
        _suspendFocusReturn = _useManualOrder;
    }

    private void SaveManualOrderState()
    {
        try
        {
            var state = new ManualOrderState
            {
                UseManualOrder = _useManualOrder,
                Orders = new Dictionary<string, int>(_manualOrderMap, StringComparer.OrdinalIgnoreCase),
                BaselineRoster = new List<string>(_manualOrderBaseline)
            };
            PersistentStorageHelper.SaveJson(ManualOrderConfigFileName, state);
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"Impossible de sauvegarder l'ordre manuel: {ex.Message}");
        }
    }

    private void ApplyManualOrderIfNeeded()
    {
        if (!ManualOrderRosterMatchesCurrentPlayers())
            return;

        EnsureManualOrderForAll();
        SortPlayersByManualOrder();
        UpdateManualOrderFromCollection();
        SaveManualOrderState();
    }

    private void EnsureManualOrderForAll()
    {
        int next = _manualOrderMap.Values.DefaultIfEmpty(-1).Max() + 1;
        foreach (var player in _playersCollection)
        {
            if (!_manualOrderMap.TryGetValue(player.Name, out var order))
            {
                order = next++;
                _manualOrderMap[player.Name] = order;
            }
            player.ManualOrder = order;
        }
    }

    private void SortPlayersByManualOrder()
    {
        var ordered = _playersCollection.OrderBy(p => p.ManualOrder).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var player = ordered[i];
            int currentIndex = _playersCollection.IndexOf(player);
            if (currentIndex != i)
            {
                _playersCollection.Move(currentIndex, i);
            }
        }
    }

    private void UpdateManualOrderFromCollection()
    {
        if (_manualOrderBaseline.Count > 0 && !ManualOrderRosterMatchesCurrentPlayers())
        {
            return;
        }

        for (int i = 0; i < _playersCollection.Count; i++)
        {
            var player = _playersCollection[i];
            player.ManualOrder = i;
            _manualOrderMap[player.Name] = i;
        }

        var names = new HashSet<string>(_playersCollection.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var key in _manualOrderMap.Keys.Where(k => !names.Contains(k)).ToList())
        {
            _manualOrderMap.Remove(key);
        }
    }

    private void ApplyManualOrderToPlayer(PlayerStats player)
    {
        if (!_manualOrderMap.TryGetValue(player.Name, out var order))
        {
            order = _manualOrderMap.Values.DefaultIfEmpty(-1).Max() + 1;
            _manualOrderMap[player.Name] = order;
        }
        player.ManualOrder = order;
    }

    private void SetManualOrderBaseline(IEnumerable<string> orderedNames)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _manualOrderBaseline = orderedNames?
            .Where(name => IsValidManualOrderName(name) && unique.Add(name))
            .ToList() ?? new List<string>();
    }

    private static bool IsValidManualOrderName(string? name)
        => !string.IsNullOrWhiteSpace(name) &&
           !string.Equals(name, "En attente...", StringComparison.OrdinalIgnoreCase);

    private bool ManualOrderRosterMatchesCurrentPlayers()
    {
        if (!_useManualOrder || _manualOrderBaseline.Count == 0)
            return false;

        var currentNames = _playersCollection
            .Select(p => p.Name)
            .Where(IsValidManualOrderName)
            .ToList();

        if (currentNames.Count != _manualOrderBaseline.Count)
            return false;

        var currentSet = new HashSet<string>(currentNames, StringComparer.OrdinalIgnoreCase);
        return currentSet.SetEquals(_manualOrderBaseline);
    }

    private void ManualOrderDialogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var entries = _playersCollection
                .Select(p =>
                {
                    int order = _manualOrderMap.TryGetValue(p.Name, out var stored) ? stored : int.MaxValue;
                    return new PlayerOrderItem(p.Name, order);
                })
                .OrderBy(item => item.Order)
                .ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Order = i;
            }

            var accentBrush = ThemeManager.AccentBrush?.CloneCurrentValue() as SolidColorBrush
                               ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xBF, 0xFF));
            if (accentBrush.CanFreeze)
            {
                accentBrush.Freeze();
            }

            var sectionBrush = (Resources["SectionBackgroundBrush"] as SolidColorBrush)?.CloneCurrentValue()
                               ?? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00));
            if (sectionBrush.CanFreeze)
            {
                sectionBrush.Freeze();
            }

            var dialog = new PlayerOrderWindow(entries, accentBrush, sectionBrush)
            {
                Owner = this
            };

            bool previousFocusState = _suspendFocusReturn;
            _suspendFocusReturn = true;

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                var orderedNames = dialog.GetOrderedNames();
                for (int i = 0; i < orderedNames.Count; i++)
                {
                    _manualOrderMap[orderedNames[i]] = i;
                }

                _useManualOrder = orderedNames.Count > 0;
                SetManualOrderBaseline(orderedNames);

                foreach (var player in _playersCollection)
                {
                    if (_manualOrderMap.TryGetValue(player.Name, out var order))
                    {
                        player.ManualOrder = order;
                    }
                }

                if (_useManualOrder)
                {
                    SortPlayersByManualOrder();
                }

                UpdateManualOrderFromCollection();
                SaveManualOrderState();
            }

            _suspendFocusReturn = previousFocusState || _useManualOrder;
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors de l'ouverture du dialogue d'ordre manuel: {ex.Message}");
        }
        finally
        {
            if (!_useManualOrder)
            {
                Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background);
            }
        }
    }

    private sealed class ManualOrderState
    {
        public bool UseManualOrder { get; set; }
        public Dictionary<string, int>? Orders { get; set; }
        public List<string>? BaselineRoster { get; set; }
    }
    
    private void ApplyKikimeterSectionBackgroundColor(string colorHex)
    {
        try
        {
            // Appliquer à la fenêtre principale
            Dispatcher.BeginInvoke(new Action(() => ApplySectionBackgroundColor(colorHex)));

            foreach (var playerWindow in _playerWindows.Values.ToList())
            {
                try
                {
                    playerWindow.ApplySectionBackgroundColor(colorHex);
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans ApplyKikimeterSectionBackgroundColor: {ex.Message}");
        }
    }
    
    private void SaveKikimeterSectionBackgroundColor(string colorHex)
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
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
            _sectionBackgroundColor = colorHex;
            
            var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, updatedJson);
            
            Logger.Info("KikimeterWindow", $"Couleur de fond des joueurs sauvegardée: {colorHex}");
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans SaveKikimeterSectionBackgroundColor: {ex.Message}");
        }
    }

    private void SavePlayerFramesOpaqueSetting(bool isOpaque)
    {
        try
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant");
            Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, "config.json");
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

            config.KikimeterPlayerFramesOpaque = isOpaque;
            if (string.IsNullOrEmpty(config.KikimeterSectionBackgroundColor))
            {
                config.KikimeterSectionBackgroundColor = _sectionBackgroundColor;
            }

            var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, updatedJson);

            Logger.Info("KikimeterWindow", $"Préférence d'opacité des cadres sauvegardée ({(isOpaque ? "opaque" : "semi-transparent")}).");
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors de la sauvegarde de l'opacité des cadres: {ex.Message}");
        }
    }
    
    private void OpenKikimeterSectionColorPicker()
    {
        try
        {
            using (var colorDialog = new FormsColorDialog())
            {
                colorDialog.FullOpen = true;
                colorDialog.Color = System.Drawing.Color.FromArgb(255, 0, 0, 0); // Noir par défaut
                
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var color = colorDialog.Color;
                    string hex = $"#FF{color.R:X2}{color.G:X2}{color.B:X2}";
                    ApplyKikimeterSectionBackgroundColor(hex);
                    SaveKikimeterSectionBackgroundColor(hex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans OpenKikimeterSectionColorPicker: {ex.Message}");
        }
    }
    
    public long MaxDamage => _displayedMaxDamage;
    public long MaxDamageTaken => _displayedMaxDamageTaken;
    public long MaxHealing => _displayedMaxHealing;
    public long MaxShield => _displayedMaxShield;
    public bool IsHorizontalMode
    {
        get => _isHorizontalMode;
        set
        {
            if (_isHorizontalMode != value)
            {
                _isHorizontalMode = value;
                OnPropertyChanged(nameof(IsHorizontalMode));
                OnPropertyChanged(nameof(PlayerModuleWidth));
                OnPropertyChanged(nameof(PlayerModuleHorizontalAlignment));
                UpdateLayoutMode();
                SaveHorizontalModeState();

                var desiredMode = _isHorizontalMode;
                _suspendWindowPersistence = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        LoadWindowPositions(desiredMode);
                    }
                    finally
                    {
                        _suspendWindowPersistence = false;
                        SaveWindowPositions(desiredMode);
                    }
                }), DispatcherPriority.Background);
            }
        }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                OnPropertyChanged(nameof(IsCollapsed));
                
                // Mise à jour synchrone (visibilité gérée directement en C# via FindName)
                UpdateTemplate();
                UpdateWindowHeight();
                UpdateMainArrowRotation();
            }
        }
    }

    public bool PlayerFramesOpaque
    {
        get => _playerFramesOpaque;
        set
        {
            if (_playerFramesOpaque != value)
            {
                _playerFramesOpaque = value;
                OnPropertyChanged(nameof(PlayerFramesOpaque));

                if (!_isLoadingPlayerFramesOpaque)
                {
                    SavePlayerFramesOpaqueSetting(value);
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyKikimeterSectionBackgroundColor(_sectionBackgroundColor);
                }), DispatcherPriority.Background);
            }
        }
    }

    public double PlayerModuleWidth => _isHorizontalMode ? 220d : double.NaN;
    public System.Windows.HorizontalAlignment PlayerModuleHorizontalAlignment => _isHorizontalMode ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Stretch;
    
    private void HorizontalModeToggle_Checked(object sender, RoutedEventArgs e) => ApplyHorizontalModeFromToggle(true, sender as ToggleButton);
    private void HorizontalModeToggle_Unchecked(object sender, RoutedEventArgs e) => ApplyHorizontalModeFromToggle(false, sender as ToggleButton);

    private void ApplyHorizontalModeFromToggle(bool enabled, ToggleButton? toggleSource)
    {
        try
        {
            if (IsHorizontalMode != enabled)
            {
                IsHorizontalMode = enabled;
            }

            if (toggleSource != null && toggleSource.IsChecked != enabled)
            {
                toggleSource.IsChecked = enabled;
            }

            Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background);
            Dispatcher.BeginInvoke(new Action(ApplyStoredWindowSize), DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"ApplyHorizontalModeFromToggle error: {ex.Message}");
        }
    }

    private void ApplyStoredWindowSize()
    {
        try
        {
            System.Windows.Size target = _isHorizontalMode ? _storedHorizontalSize : _storedVerticalSize;
            if (target.Width > 0 && target.Height > 0)
            {
                Width = target.Width;
                Height = target.Height;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"ApplyStoredWindowSize error: {ex.Message}");
        }
    }

    private void KikimeterWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        if (_isHorizontalMode)
        {
            _storedHorizontalSize = e.NewSize;
        }
        else
        {
            _storedVerticalSize = e.NewSize;
        }
    }
    
    private void KikimeterWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Les gestionnaires sont déjà attachés dans InitializeComponent
            // Vérifier que les Border sont bien initialisés et forcer leur visibilité
            ForceArrowVisibility();
            
            // Mettre à jour la rotation des flèches selon l'état initial
            UpdateMainArrowRotation();
            
            // Ajouter des gestionnaires d'événements pour retourner le focus au jeu après chaque interaction
            this.MouseDown += (s, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.MouseUp += (s, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.KeyDown += (s, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.KeyUp += (s, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.PreviewMouseDown += (s, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            this.PreviewMouseUp += (s, ev) => { Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background); };
            
            // IndividualModeCheckbox supprimé - mode individuel désactivé
            // if (IndividualModeCheckbox != null)
            // {
            //     IndividualModeCheckbox supprimé
            //     string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "kikimeter_individual_mode.json");
            //     if (File.Exists(configFile))
            //     {
            //         Dictionary<string, bool> config = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(configFile));
            //         if (config != null && config.ContainsKey("IndividualMode") && config["IndividualMode"])
            //         {
            //             Logger.Info("KikimeterWindow", "Mode individuel sauvegardé détecté, activation automatique");
            //             // Mode individuel désactivé
            //             Dispatcher.BeginInvoke(new Action(() =>
            //             {
            //                 try
            //                 {
            //                     // IndividualModeCheckbox_Click désactivé
            //                 }
            //                 catch (Exception ex2)
            //                 {
            //                     Logger.Error("KikimeterWindow", $"Erreur lors de l'activation du mode individuel sauvegardé: {ex2.Message}");
            //                 }
            //             }), DispatcherPriority.Loaded, Array.Empty<object>());
            //         }
            //         else
            //         {
            //             IndividualModeCheckbox.IsChecked = false;
            //             Logger.Info("KikimeterWindow", "Mode fenêtre complète (par défaut ou sauvegardé)");
            //         }
            //     }
            //     else
            //     {
            //         IndividualModeCheckbox.IsChecked = false;
            //         SaveIndividualModeState();
            //         Logger.Info("KikimeterWindow", "Mode fenêtre complète par défaut (aucune sauvegarde)");
            //     }
            // }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur KikimeterWindow_Loaded: {ex.Message}");
        }
    }
    
    private void KikimeterWindow_SourceInitialized(object sender, EventArgs e)
    {
        WindowInteropHelper helper = new WindowInteropHelper(this);
        HwndSource hwndSource = HwndSource.FromHwnd(helper.Handle);
        if (hwndSource != null)
        {
            hwndSource.AddHook(WindowProc);
        }
        ExcludeFromAltTab(helper.Handle);
    }
    
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
    
    private void ExcludeFromAltTab(IntPtr hwnd)
    {
        try
        {
            uint extendedStyle = GetWindowLong(hwnd, -20);
            extendedStyle |= 128U;
            SetWindowLong(hwnd, -20, extendedStyle);
        }
        catch { }
    }
    
    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 562)
        {
            ReturnFocusToGame();
            return IntPtr.Zero;
        }
        if (msg != 132)
        {
            return IntPtr.Zero;
        }
        System.Windows.Point pt = new System.Windows.Point((double)((int)lParam & 65535), (double)((int)lParam >> 16));
        System.Windows.Point windowPt = PointFromScreen(pt);
        Rect rect = new Rect(RenderSize);
        if (windowPt.X <= 5.0 && windowPt.Y <= 5.0)
        {
            handled = true;
            return new IntPtr(13);
        }
        if (windowPt.X >= rect.Width - 5.0 && windowPt.Y <= 5.0)
        {
            handled = true;
            return new IntPtr(14);
        }
        if (windowPt.X <= 5.0 && windowPt.Y >= rect.Height - 5.0)
        {
            handled = true;
            return new IntPtr(16);
        }
        if (windowPt.X >= rect.Width - 5.0 && windowPt.Y >= rect.Height - 5.0)
        {
            handled = true;
            return new IntPtr(17);
        }
        if (windowPt.X <= 5.0)
        {
            handled = true;
            return new IntPtr(10);
        }
        if (windowPt.X >= rect.Width - 5.0)
        {
            handled = true;
            return new IntPtr(11);
        }
        if (windowPt.Y <= 5.0)
        {
            handled = true;
            return new IntPtr(12);
        }
        if (windowPt.Y >= rect.Height - 5.0)
        {
            handled = true;
            return new IntPtr(15);
        }
        HitTestResult hitTestResult = VisualTreeHelper.HitTest(this, windowPt);
        if (hitTestResult != null)
        {
            DependencyObject current = hitTestResult.VisualHit;
            while (current != null && current != this)
            {
                if (current is System.Windows.Controls.Button || current is System.Windows.Controls.ProgressBar || current is ScrollViewer || current is ItemsControl || current is System.Windows.Controls.CheckBox)
                {
                    return IntPtr.Zero;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        handled = true;
        return new IntPtr(1);
    }
    
    private void UpdateAccentBrushResource()
    {
        try
        {
            SolidColorBrush accentBrush = ThemeManager.AccentBrush;
            Resources["CyanAccentBrush"] = accentBrush;
        }
        catch { }
    }
    
    private void LoadSectionBackgroundColor()
    {
        try
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
            if (!File.Exists(configFile))
            {
                ApplySectionBackgroundColor(_sectionBackgroundColor);
                return;
            }

            var json = File.ReadAllText(configFile);
            Config? config = JsonConvert.DeserializeObject<Config>(json);
            if (config == null)
            {
                ApplySectionBackgroundColor(_sectionBackgroundColor);
                return;
            }

            _isLoadingPlayerFramesOpaque = true;
            PlayerFramesOpaque = config.KikimeterPlayerFramesOpaque;
            _isLoadingPlayerFramesOpaque = false;

            if (!string.IsNullOrEmpty(config.KikimeterSectionBackgroundColor))
            {
                ApplySectionBackgroundColor(config.KikimeterSectionBackgroundColor);
                return;
            }

            ApplySectionBackgroundColor(_sectionBackgroundColor);
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur chargement couleur sections: {ex.Message}");
        }
    }
    
    public void ApplySectionBackgroundColor(string colorHex)
    {
        try
        {
            _sectionBackgroundColor = string.IsNullOrWhiteSpace(colorHex) ? _sectionBackgroundColor : colorHex;
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_sectionBackgroundColor);

            if (PlayerFramesOpaque)
            {
                color.A = 255;
            }
            else if (color.A == 255)
            {
                color.A = 128;
            }

            SolidColorBrush brush = new SolidColorBrush(color);
            Resources["SectionBackgroundBrush"] = brush;
            Logger.Info("KikimeterWindow", $"Couleur sections appliquée: {_sectionBackgroundColor} (alpha: {color.A})");
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur application couleur sections {colorHex}: {ex.Message}");
        }
    }
    
    public void SetZoom(double zoomValue)
    {
        if (WindowScale != null)
        {
            WindowScale.ScaleX = zoomValue;
            WindowScale.ScaleY = zoomValue;
            Logger.Debug("KikimeterWindow", $"Zoom modifié: {(int)(zoomValue * 100)}%");
        }
    }

    private void InitializeWindow()
    {
        LoadWindowPositions();
        ApplySectionColor();
        
        LocationChanged += (s, e) => 
        {
            SaveWindowPositions();
            ReturnFocusToGame();
        };
        SizeChanged += (s, e) => 
        {
            SaveWindowPositions();
            // Utiliser un délai pour éviter trop d'appels pendant le redimensionnement
            Dispatcher.BeginInvoke(new Action(() => ReturnFocusToGame()), DispatcherPriority.Background);
        };
        Closing += (s, e) => SaveWindowPositions();
        
        // Le timer est maintenant dans StartWatching()
        
        // Le mode individuel sera géré par KikimeterWindow_Loaded
        // L'orientation sera aussi chargée dans KikimeterWindow_Loaded
    }

    private void LoadIndividualMode()
    {
        try
        {
            var modePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kikimeter_individual_mode.json");
            if (File.Exists(modePath))
            {
                var json = File.ReadAllText(modePath);
                _individualMode = JsonConvert.DeserializeObject<KikimeterIndividualMode>(json) ?? new KikimeterIndividualMode();
                _isIndividualMode = _individualMode.IndividualMode;
                Logger.Info("KikimeterWindow", $"Mode individuel sauvegardé: {_isIndividualMode}");
                
                if (_isIndividualMode)
                {
                    Logger.Info("KikimeterWindow", "Mode individuel sauvegardé détecté, activation automatique");
                }
            }
        }
        catch { }
    }

    private void LoadXpWindowStates()
    {
        try
        {
            var stored = PersistentStorageHelper.LoadJsonWithFallback<XpWindowStateCollection>(XpWindowStateFileName);
            if (stored?.Windows != null)
            {
                _xpWindowStates = new Dictionary<string, XpWindowState>(stored.Windows, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"Impossible de charger la configuration des fenêtres XP: {ex.Message}");
            _xpWindowStates = new Dictionary<string, XpWindowState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveXpWindowStates()
    {
        try
        {
            var container = new XpWindowStateCollection
            {
                Windows = new Dictionary<string, XpWindowState>(_xpWindowStates, StringComparer.OrdinalIgnoreCase)
            };
            PersistentStorageHelper.SaveJson(XpWindowStateFileName, container);
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"Impossible de sauvegarder la configuration des fenêtres XP: {ex.Message}");
        }
    }

    private bool _isChangingMode = false;
    
    // IndividualModeCheckbox supprimé - mode individuel désactivé
    private void IndividualModeCheckbox_Click(object sender, RoutedEventArgs e)
    {
        // Mode individuel désactivé - méthode conservée pour compatibilité mais ne fait rien
        // Le mode individuel a été supprimé de l'UI
    }
    
    public void SetIndividualMode(bool enabled, bool suppressEvent = false)
    {
        if (_isChangingMode && !suppressEvent)
            return;
            
        try
        {
            if (suppressEvent)
            {
                _isChangingMode = true;
            }
            
            // IndividualModeCheckbox supprimé - mode individuel désactivé
            // if (IndividualModeCheckbox != null)
            // {
            //     IndividualModeCheckbox.IsChecked = enabled;
            // }
            // 
            // if (!suppressEvent)
            // {
            //     // L'événement Click sera déclenché automatiquement
            //     IndividualModeCheckbox_Click(IndividualModeCheckbox, new RoutedEventArgs());
            // }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans SetIndividualMode: {ex.Message}");
        }
        finally
        {
            if (suppressEvent)
            {
                _isChangingMode = false;
            }
        }
    }
    
    public void CloseAllIndividualWindows()
    {
        try
        {
            // Fermer toutes les fenêtres individuelles
            foreach (KeyValuePair<string, Views.PlayerWindow> kvp in _playerWindows.ToList())
            {
                try
                {
                    kvp.Value.Close();
                }
                catch { }
            }
            _playerWindows.Clear();
            
            // Fermer la fenêtre indicateur si elle existe
            if (_indicatorWindow != null)
            {
                try
                {
                    _indicatorWindow.Close();
                    _indicatorWindow = null;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans CloseAllIndividualWindows: {ex.Message}");
        }
    }
    
    private void CreateIndividualPlayerWindow(PlayerStats player)
    {
        try
        {
            if (!_playerWindows.ContainsKey(player.Name))
            {
                if (_playerWindows.ContainsKey("INDICATOR_EMPTY"))
                {
                    _playerWindows["INDICATOR_EMPTY"].Close();
                    _playerWindows.Remove("INDICATOR_EMPTY");
                }
                Views.PlayerWindow playerWindow = new Views.PlayerWindow(player, _displayedMaxDamage, _displayedMaxDamageTaken, _displayedMaxHealing, _displayedMaxShield, null);
                _playerWindows[player.Name] = playerWindow;
                string sectionColor = GetSectionBackgroundColor();
                if (!string.IsNullOrEmpty(sectionColor))
                {
                    playerWindow.ApplySectionBackgroundColor(sectionColor);
                }
                WindowPosition savedPosition = LoadSavedPlayerWindowPosition(player.Name);
                if (savedPosition != null)
                {
                    playerWindow.Left = savedPosition.Left;
                    playerWindow.Top = savedPosition.Top;
                    playerWindow.Width = savedPosition.Width > 0 ? savedPosition.Width : playerWindow.Width;
                    playerWindow.Height = savedPosition.Height > 0 ? savedPosition.Height : playerWindow.Height;
                }
                else
                {
                    int index = _playersCollection.IndexOf(player);
                    playerWindow.Left = Left + 50.0 + (index * 30);
                    playerWindow.Top = Top + 100.0 + (index * 30);
                }
                playerWindow.SizeChanged += (s, args) => SavePlayerWindowPosition(player.Name, playerWindow);
                playerWindow.LocationChanged += (s, args) => SavePlayerWindowPosition(player.Name, playerWindow);
                playerWindow.MouseLeftButtonUp += (s, args) => SavePlayerWindowPosition(player.Name, playerWindow);
                playerWindow.Closing += (s, args) => SavePlayerWindowPosition(player.Name, playerWindow);
                playerWindow.Show();
                if (_playerWindows.Count == 1)
                {
                    HideFromController(false);
                }
                Logger.Info("KikimeterWindow", $"Fenêtre individuelle créée automatiquement pour {player.Name}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur CreateIndividualPlayerWindow pour {player.Name}: {ex.Message}");
        }
    }
    
    private void CreateEmptyIndicatorWindow()
    {
        try
        {
            PlayerStats dummyPlayer = new PlayerStats
            {
                Name = "En attente..."
            };
            Views.PlayerWindow indicatorWindow = new Views.PlayerWindow(dummyPlayer, _displayedMaxDamage, _displayedMaxDamageTaken, _displayedMaxHealing, _maxShield, null);
            string indicatorKey = "INDICATOR_EMPTY";
            _playerWindows[indicatorKey] = indicatorWindow;
            WindowPosition savedPosition = LoadSavedPlayerWindowPosition(indicatorKey);
            if (savedPosition != null)
            {
                indicatorWindow.Left = savedPosition.Left;
                indicatorWindow.Top = savedPosition.Top;
                indicatorWindow.Width = savedPosition.Width > 0 ? savedPosition.Width : indicatorWindow.Width;
                indicatorWindow.Height = savedPosition.Height > 0 ? savedPosition.Height : indicatorWindow.Height;
            }
            else
            {
                indicatorWindow.Left = Left + 150.0;
                indicatorWindow.Top = Top + 150.0;
            }
            indicatorWindow.SizeChanged += (s, args) => SavePlayerWindowPosition(indicatorKey, indicatorWindow);
            indicatorWindow.LocationChanged += (s, args) => SavePlayerWindowPosition(indicatorKey, indicatorWindow);
            indicatorWindow.MouseLeftButtonUp += (s, args) => SavePlayerWindowPosition(indicatorKey, indicatorWindow);
            indicatorWindow.Closing += (s, args) => SavePlayerWindowPosition(indicatorKey, indicatorWindow);
            indicatorWindow.Show();
            HideFromController(false);
            Logger.Info("KikimeterWindow", "Fenêtre d'indication vide créée pour le mode individuel");
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur CreateEmptyIndicatorWindow: {ex.Message}");
        }
    }
    
    private WindowPosition LoadSavedPlayerWindowPosition(string playerName)
    {
        try
        {
            string positionsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "player_window_positions.json");
            if (File.Exists(positionsFile))
            {
                Dictionary<string, WindowPosition> positions = JsonConvert.DeserializeObject<Dictionary<string, WindowPosition>>(File.ReadAllText(positionsFile));
                if (positions != null && positions.ContainsKey(playerName))
                {
                    return positions[playerName];
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur LoadSavedPlayerWindowPosition pour {playerName}: {ex.Message}");
        }
        return null;
    }
    
    private void SavePlayerWindowPosition(string playerName, Views.PlayerWindow window)
    {
        try
        {
            string positionsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "player_window_positions.json");
            string positionsDir = Path.GetDirectoryName(positionsFile);
            if (!Directory.Exists(positionsDir))
            {
                Directory.CreateDirectory(positionsDir);
            }
            Dictionary<string, WindowPosition> positions = new Dictionary<string, WindowPosition>();
            if (File.Exists(positionsFile))
            {
                positions = JsonConvert.DeserializeObject<Dictionary<string, WindowPosition>>(File.ReadAllText(positionsFile)) ?? new Dictionary<string, WindowPosition>();
            }
            positions[playerName] = new WindowPosition
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height
            };
            string newJson = JsonConvert.SerializeObject(positions, Formatting.Indented);
            File.WriteAllText(positionsFile, newJson);
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur SavePlayerWindowPosition pour {playerName}: {ex.Message}");
        }
    }
    
    private string GetSectionBackgroundColor()
    {
        try
        {
            string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "config.json");
            if (File.Exists(configFile))
            {
                Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFile));
                if (config != null && !string.IsNullOrEmpty(config.KikimeterSectionBackgroundColor))
                {
                    return config.KikimeterSectionBackgroundColor;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur récupération couleur sections: {ex.Message}");
        }
        return "#FF000000";
    }

    private void ShowIndividualWindows()
    {
        // Afficher la fenêtre d'indication si aucune fenêtre de joueur
        if (_playersCollection.Count == 0)
        {
            CreateEmptyIndicatorWindow();
        }
        else
        {
            _indicatorWindow?.Hide();
        }
        
        foreach (var player in _playersCollection)
        {
            if (!_playerWindows.ContainsKey(player.Name))
            {
                CreateIndividualPlayerWindow(player);
            }
            else
            {
                _playerWindows[player.Name].Show();
            }
        }
    }

    private void HideIndividualWindows()
    {
        foreach (var window in _playerWindows.Values)
        {
            window.Hide();
        }
        _indicatorWindow?.Hide();
    }

    public void ToggleToNormalMode()
    {
        _isIndividualMode = false;
        _individualMode.IndividualMode = false;
        SaveIndividualModeState();
        
        ShowFromController(true);
        HideIndividualWindows();
        // IndividualModeCheckbox supprimé - mode individuel désactivé
        // if (IndividualModeCheckbox != null)
        // {
        //     IndividualModeCheckbox.IsChecked = false;
        // }
    }

    private void SaveIndividualModeState()
    {
        try
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amaliassistant", "kikimeter_individual_mode.json");
            string configDir = Path.GetDirectoryName(text);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            Dictionary<string, bool> config = new Dictionary<string, bool>();
                config["IndividualMode"] = false; // IndividualModeCheckbox supprimé - mode individuel désactivé
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(text, json);
            Logger.Info("KikimeterWindow", $"Mode individuel sauvegardé: {config["IndividualMode"]}");
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur SaveIndividualModeState: {ex.Message}");
        }
    }

    private void LoadWindowPositions(bool? horizontalOverride = null)
    {
        try
        {
            var positions = PersistentStorageHelper.LoadJsonWithFallback<WindowPositions>("window_positions.json");
            bool horizontal = horizontalOverride ?? _isHorizontalMode;

            WindowPosition? targetPosition = horizontal
                ? positions.KikimeterWindowHorizontal ?? positions.KikimeterWindow
                : positions.KikimeterWindowVertical ?? positions.KikimeterWindow;

            if (targetPosition == null)
            {
                return;
            }

            _suspendWindowPersistence = true;
            try
            {
                Left = targetPosition.Left;
                Top = targetPosition.Top;
                if (targetPosition.Width > 0)
                {
                    Width = Math.Max(Math.Min(targetPosition.Width, MaxWidth), MinWidth);
                }
                if (targetPosition.Height > 0)
                {
                    Height = Math.Max(Math.Min(targetPosition.Height, MaxHeight), MinHeight);
                }
            }
            finally
            {
                _suspendWindowPersistence = false;
            }

            if (horizontal)
            {
                _storedHorizontalSize = new System.Windows.Size(Width, Height);
            }
            else
            {
                _storedVerticalSize = new System.Windows.Size(Width, Height);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"LoadWindowPositions error: {ex.Message}");
        }
    }

    private void SaveWindowPositions(bool? horizontalOverride = null)
    {
        if (_suspendWindowPersistence)
        {
            return;
        }

        try
        {
            var positions = PersistentStorageHelper.LoadJsonWithFallback<WindowPositions>("window_positions.json");
            bool horizontal = horizontalOverride ?? _isHorizontalMode;

            var snapshot = new WindowPosition
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            };

            if (horizontal)
            {
                positions.KikimeterWindowHorizontal = snapshot;
            }
            else
            {
                positions.KikimeterWindowVertical = snapshot;
            }

            positions.KikimeterWindow = snapshot; // compatibilité ancienne version

            PersistentStorageHelper.SaveJson("window_positions.json", positions);
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"SaveWindowPositions error: {ex.Message}");
        }
    }

    private void ApplySectionColor()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);
                if (config != null)
                {
                    // Appliquer la couleur de section (seulement pour les sections internes, pas le MainBorder)
                    if (!string.IsNullOrEmpty(config.KikimeterSectionBackgroundColor))
                    {
                        ApplyKikimeterSectionBackgroundColor(config.KikimeterSectionBackgroundColor);
                    }
                    
                    // Appliquer le background de fenêtre si activé
                    if (config.KikimeterWindowBackgroundEnabled)
                    {
                        try
                        {
                            var bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(config.KikimeterWindowBackgroundColor);
                            bgColor.A = (byte)(config.KikimeterWindowBackgroundOpacity * 255);
                            this.Background = new System.Windows.Media.SolidColorBrush(bgColor);
                            // Si le background de fenêtre est activé, appliquer aussi au MainBorder
                            if (MainBorder != null)
                            {
                                MainBorder.Background = new System.Windows.Media.SolidColorBrush(bgColor);
                            }
                            Logger.Info("KikimeterWindow", $"Background de fenêtre appliqué: {config.KikimeterWindowBackgroundColor} avec opacité {config.KikimeterWindowBackgroundOpacity}");
                        }
                        catch { }
                    }
                    else
                    {
                        this.Background = System.Windows.Media.Brushes.Transparent;
                        // S'assurer que le MainBorder reste transparent
                        if (MainBorder != null)
                        {
                            MainBorder.Background = System.Windows.Media.Brushes.Transparent;
                        }
                    }
                }
            }
        }
        catch { }
    }

    public void StartWatching()
    {
        // Protection contre les initialisations multiples
        if (_isInitializing)
        {
            Logger.Warning("KikimeterWindow", "StartWatching déjà en cours, ignoré");
            return;
        }

        if (_isResetInProgress)
        {
            Logger.Warning("KikimeterWindow", "Reset en cours, StartWatching ignoré");
            return;
        }

        _isInitializing = true;

        try
        {
            Logger.Info("KikimeterWindow", $"Démarrage de la surveillance du log: '{_logPath}'");
            
            if (string.IsNullOrEmpty(_logPath))
            {
                Logger.Error("KikimeterWindow", "Le chemin de log est vide ou null ! Veuillez configurer le chemin dans les paramètres.");
                return;
            }
            
            // Initialiser le fichier JSON AVANT tout (ne bloque jamais le démarrage)
            PlayerDataJsonInitializer.EnsurePlayerDataJsonExists();
            
            // Ne plus retourner si le fichier n'existe pas - LogFileWatcher peut maintenant surveiller
            // même si le fichier n'existe pas encore (il détectera sa création)
            if (!File.Exists(_logPath))
            {
                Logger.Info("KikimeterWindow", $"Le fichier de log n'existe pas encore: '{_logPath}'. La surveillance sera activée et détectera sa création.");
            }
            
            // Arrêter l'ancien watcher s'il existe avant d'en créer un nouveau
            if (_logWatcher != null)
        {
            Logger.Info("KikimeterWindow", "Arrêt de l'ancien watcher avant de créer un nouveau");
            try
            {
                // Désabonner les événements avant de disposer
                _logWatcher.Parser.PlayerAdded -= OnPlayerAdded;
                _logWatcher.Parser.CombatStarted -= OnCombatStarted;
                _logWatcher.Parser.CombatEnded -= OnCombatEnded;
                _logWatcher.LogLineProcessed -= OnLogLineProcessed;
                _logWatcher.ErrorOccurred -= OnWatcherError;
                _logWatcher.LogFileNotFound -= OnLogFileNotFound;
            }
            catch (Exception ex)
            {
                Logger.Warning("KikimeterWindow", $"Erreur lors du désabonnement des événements: {ex.Message}");
            }
            
            _logWatcher.StopWatching();
            _logWatcher.Dispose();
            _logWatcher = null;
        }
        
        // Arrêter l'ancien timer s'il existe
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= UpdateTimer_Tick;
            _updateTimer = null;
        }
        
        _logWatcher = new Services.LogFileWatcher();
        _logWatcher.StartWatching(_logPath);
        
        if (_xpCoordinator != null)
        {
            _xpCoordinator.ExperienceGained -= OnExperienceGained;
            _xpCoordinator.Dispose();
        }
        _xpCoordinator = new XpTrackingCoordinator(_logWatcher);
        _xpCoordinator.ExperienceGained += OnExperienceGained;
        
        // Initialiser le service de gestion des joueurs basé sur le polling JSON
        // Essayer d'abord avec JsonPlayerDataProvider, sinon fallback sur LogParserPlayerDataProvider
        try
        {
            _playerDataProvider = new Services.JsonPlayerDataProvider();
            Logger.Info("KikimeterWindow", "JsonPlayerDataProvider initialisé avec succès");
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"Impossible d'initialiser JsonPlayerDataProvider, utilisation du fallback: {ex.Message}");
            _playerDataProvider = new Services.LogParserPlayerDataProvider(_logWatcher.Parser);
        }
        
        _playerManagementService = new Services.PlayerManagementService(_playerDataProvider);
        
        // Définir la collection de joueurs dans le service central (SOURCE UNIQUE DE VÉRITÉ)
        _playerManagementService.SetPlayersCollection(_playersCollection);
        
        // S'abonner aux events du service pour notifier les autres composants
        _playerManagementService.PlayersChanged += OnPlayersChangedFromService;
        _playerManagementService.MainCharacterChanged += OnMainCharacterChangedFromService;
        
        // S'abonner aux événements du parser
        _logWatcher.Parser.PlayerAdded += OnPlayerAdded;
        _logWatcher.Parser.CombatStarted += OnCombatStarted;
        _logWatcher.Parser.CombatEnded += OnCombatEnded;
        
        Logger.Info("KikimeterWindow", $"Événements du parser abonnés. ItemsControl null? {PlayersItemsControl == null}, ItemsSource null? {PlayersItemsControl?.ItemsSource == null}");
        
        // S'assurer que l'ItemsSource est bien assigné
        if (PlayersItemsControl != null && PlayersItemsControl.ItemsSource != _playersCollection)
        {
            Logger.Warning("KikimeterWindow", "ItemsSource n'était pas correctement assigné, correction...");
            PlayersItemsControl.ItemsSource = _playersCollection;
        }
        
        // Mettre à jour la liste des joueurs du combat dans LootWindow (déjà fait dans OnPlayerAdded)
        
        // S'abonner aux lignes de log pour détecter les kikis (méthode principale pour cette version)
        _logWatcher.LogLineProcessed += OnLogLineProcessed;
        
        // S'abonner aux erreurs du watcher
        _logWatcher.ErrorOccurred += OnWatcherError;
        _logWatcher.LogFileNotFound += OnLogFileNotFound;
        
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
        
        _isInitialized = true;
        Logger.Info("KikimeterWindow", "Surveillance du log démarrée avec succès");
        }
        finally
        {
            _isInitializing = false;
        }
    }
    
    public void UpdateLogPath(string newLogPath)
    {
        if (string.IsNullOrEmpty(newLogPath))
        {
            Logger.Warning("KikimeterWindow", "Tentative de mise à jour avec un chemin de log vide");
            return;
        }
        
        // Ne plus retourner si le fichier n'existe pas - LogFileWatcher peut maintenant surveiller
        // même si le fichier n'existe pas encore (il détectera sa création)
        if (!File.Exists(newLogPath))
        {
            Logger.Info("KikimeterWindow", $"Le nouveau chemin de log n'existe pas encore: '{newLogPath}'. La surveillance sera activée et détectera sa création.");
        }
        
        Logger.Info("KikimeterWindow", $"Mise à jour du chemin de log: '{_logPath}' -> '{newLogPath}'");
        
        // Arrêter l'ancienne surveillance
        if (_logWatcher != null)
        {
            _logWatcher.StopWatching();
            _logWatcher.Dispose();
            _logWatcher = null;
        }
        
        // Mettre à jour le chemin
        _logPath = newLogPath;
        
        // Redémarrer la surveillance avec le nouveau chemin
        StartWatching();
    }
    
    private void OnPlayerAdded(object sender, PlayerStats player)
    {
        if (player == null)
        {
            Logger.Warning("KikimeterWindow", "OnPlayerAdded appelé avec un joueur null");
            return;
        }
        
        Logger.Info("KikimeterWindow", $"OnPlayerAdded appelé pour: {player.Name}");
        
        Dispatcher.Invoke(() =>
        {
            try
        {
            var existing = _playersCollection.FirstOrDefault(p => string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                    Logger.Info("KikimeterWindow", $"Ajout du nouveau joueur: {player.Name} (Total avant: {_playersCollection.Count})");
                
                // Initialiser les propriétés de gestion des joueurs depuis le polling JSON
                if (_playerDataProvider != null)
                {
                    var currentPlayers = _playerDataProvider.GetCurrentPlayers();
                    var playerData = currentPlayers.Values.FirstOrDefault(p => 
                        string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Id, player.PlayerId.ToString(), StringComparison.OrdinalIgnoreCase));
                    
                    if (playerData != null)
                    {
                        player.IsInGroup = playerData.IsInGroup;
                        player.IsMainCharacter = playerData.IsMainCharacter;
                        player.IsActive = playerData.IsActive;
                        player.LastSeenInCombat = playerData.LastSeenInCombat != DateTime.MinValue 
                            ? playerData.LastSeenInCombat 
                            : DateTime.Now;
                    }
                    else
                    {
                        // Valeurs par défaut si non trouvé dans le JSON
                        player.IsInGroup = false;
                        player.IsMainCharacter = false;
                        player.IsActive = true;
                        player.LastSeenInCombat = DateTime.Now;
                    }
                }
                else
                {
                    // Valeurs par défaut si le service n'est pas encore initialisé
                    player.IsInGroup = false;
                    player.IsMainCharacter = false;
                    player.IsActive = true;
                    player.LastSeenInCombat = DateTime.Now;
                }
                
                _playersCollection.Add(player);
                    Logger.Info("KikimeterWindow", $"Joueur ajouté avec succès. Total maintenant: {_playersCollection.Count}");
                player.PropertyChanged += Player_PropertyChanged;
                    
                    // Vérifier que l'ItemsControl est bien lié
                    if (PlayersItemsControl != null && PlayersItemsControl.ItemsSource != _playersCollection)
                    {
                        Logger.Warning("KikimeterWindow", "ItemsSource n'est pas correctement lié, correction en cours...");
                        PlayersItemsControl.ItemsSource = _playersCollection;
                    }
                    
                // IndividualModeCheckbox supprimé - mode individuel désactivé
                if (false) // IndividualModeCheckbox != null && IndividualModeCheckbox.IsChecked.GetValueOrDefault()
                {
                    CreateIndividualPlayerWindow(player);
                }
                
                if (_useManualOrder)
                {
                    ApplyManualOrderIfNeeded();
                }
                UpdateLootWindowCombatPlayers();
                    UpdatePreviewVisibility();
                    UpdateWindowHeight();
            }
            else if (!ReferenceEquals(existing, player))
            {
                    Logger.Info("KikimeterWindow", $"Mise à jour du joueur existant: {player.Name}");
                int index = _playersCollection.IndexOf(existing);
                existing.PropertyChanged -= Player_PropertyChanged;
                player.ManualOrder = existing.ManualOrder;
                _manualOrderMap[player.Name] = player.ManualOrder;
                _playersCollection[index] = player;
                player.PropertyChanged += Player_PropertyChanged;
                if (_useManualOrder)
                {
                    ApplyManualOrderIfNeeded();
                }
                UpdateLootWindowCombatPlayers();
                    UpdatePreviewVisibility();
                    UpdateWindowHeight();
                }
                else
                {
                    Logger.Debug("KikimeterWindow", $"Joueur {player.Name} déjà présent (même référence)");
                }
                
                // La flèche est maintenant en position fixe à côté de la croix, plus besoin de mise à jour
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Erreur dans OnPlayerAdded pour {player.Name}: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }
    
    private void OnExperienceGained(object? sender, XpGainEvent e)
    {
        _lastXpEvents[e.EntityName] = e;
        Dispatcher.InvokeAsync(() => UpdateXpWindow(e.EntityName, e));
    }
    
    private void UpdateXpWindow(string entityName, XpGainEvent? xpEvent)
    {
        if (string.IsNullOrWhiteSpace(entityName) || _xpCoordinator == null)
            return;

        var entry = _xpCoordinator.GetEntry(entityName);
        entry ??= CreateEmptyXpEntry(entityName);

        if (!_xpWindowStates.TryGetValue(entityName, out var state))
        {
            state = new XpWindowState { EntityName = entityName };
            _xpWindowStates[entityName] = state;
            SaveXpWindowStates();
        }

        if (xpEvent == null && _lastXpEvents.TryGetValue(entityName, out var last))
        {
            xpEvent = last;
        }

        if (state.IsVisible && !_xpWindows.ContainsKey(entityName))
        {
            ShowXpWindow(entityName, ensureVisible: false);
        }

        if (_xpWindows.TryGetValue(entityName, out var windowInstance))
        {
            if (xpEvent != null)
            {
                windowInstance.ViewModel.ApplyEntry(entry, xpEvent);
            }
            else
            {
                windowInstance.ViewModel.ApplyEntry(entry, null);
            }
        }
    }

    private void ShowXpWindow(string entityName, bool ensureVisible = true)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return;

        if (_xpWindows.TryGetValue(entityName, out var existing))
        {
            var entry = _xpCoordinator?.GetEntry(entityName) ?? CreateEmptyXpEntry(entityName);
            existing.ViewModel.ApplyEntry(entry, _lastXpEvents.TryGetValue(entityName, out var evt) ? evt : null);

            if (ensureVisible)
            {
                existing.Show();
                existing.Activate();
                UpdateXpWindowState(entityName, state =>
                {
                    state.IsVisible = true;
                    state.Left = existing.Left;
                    state.Top = existing.Top;
                    state.Width = existing.Width;
                    state.Height = existing.Height;
                });
            }
            return;
        }

        var viewModel = new XpProgressViewModel(entityName);
        var window = new XpProgressWindow(viewModel);

        window.HideRequested += (_, _) => HideXpWindow(entityName);
        window.ResetRequested += XpWindow_ResetRequested;
        window.ColorChanged += XpWindow_ColorChanged;
        window.ResetAllRequested += XpWindow_ResetAllRequested;
        window.LocationChanged += XpWindow_LocationOrSizeChanged;
        window.SizeChanged += XpWindow_LocationOrSizeChanged;
        window.Closing += (s, e) =>
        {
            e.Cancel = true;
            HideXpWindow(entityName);
        };

        _xpWindows[entityName] = window;

        if (_xpWindowStates.TryGetValue(entityName, out var storedState))
        {
            ApplyXpWindowState(window, storedState);
        }
        else
        {
            var newState = new XpWindowState { EntityName = entityName, IsVisible = ensureVisible };
            _xpWindowStates[entityName] = newState;
            SaveXpWindowStates();
        }

        UpdateXpWindow(entityName, null);

        if (ensureVisible)
        {
            window.Show();
            var entry = _xpCoordinator?.GetEntry(entityName) ?? CreateEmptyXpEntry(entityName);
            window.ViewModel.ApplyEntry(entry, _lastXpEvents.TryGetValue(entityName, out var evt) ? evt : null);
            window.Activate();
            UpdateXpWindowState(entityName, state =>
            {
                state.IsVisible = true;
                state.Left = window.Left;
                state.Top = window.Top;
                state.Width = window.Width;
                state.Height = window.Height;
            });
        }
        else if (_xpWindowStates.TryGetValue(entityName, out var state) && state.IsVisible)
        {
            window.Show();
            window.Activate();
        }
    }

    private void HideXpWindow(string entityName)
    {
        if (!_xpWindows.TryGetValue(entityName, out var window))
            return;

        window.Hide();
        UpdateXpWindowState(entityName, state => state.IsVisible = false);
    }

    private void ApplyXpWindowState(XpProgressWindow window, XpWindowState state)
    {
        if (!double.IsNaN(state.Left) && !double.IsNaN(state.Top))
        {
            window.Left = state.Left;
            window.Top = state.Top;
        }

        if (state.Width > 0)
        {
            window.Width = state.Width;
        }

        if (state.Height > 0)
        {
            window.Height = state.Height;
        }

        if (!string.IsNullOrWhiteSpace(state.ProgressColor))
        {
            window.ViewModel.ApplyColor(state.ProgressColor);
        }
    }

    private void XpWindow_LocationOrSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not XpProgressWindow window)
            return;

        var entityName = window.ViewModel.EntityName;
        UpdateXpWindowState(entityName, state =>
        {
            state.Left = window.Left;
            state.Top = window.Top;
            state.Width = window.Width;
            state.Height = window.Height;
        });
    }

    private void XpWindow_ColorChanged(object? sender, string colorHex)
    {
        if (sender is not XpProgressWindow window)
            return;

        window.ViewModel.ApplyColor(colorHex);
        UpdateXpWindowState(window.ViewModel.EntityName, state => state.ProgressColor = colorHex);
    }

    private void XpWindow_ResetAllRequested(object? sender, EventArgs e)
    {
        ResetAllXpWindowPreferences();
    }

    private void XpWindow_ResetRequested(object? sender, EventArgs e)
    {
        if (sender is not XpProgressWindow window)
            return;

        ResetXpTracking(window.ViewModel.EntityName, window);
    }

    private void UpdateXpWindowState(string entityName, Action<XpWindowState> updater)
    {
        if (!_xpWindowStates.TryGetValue(entityName, out var state))
        {
            state = new XpWindowState { EntityName = entityName };
            _xpWindowStates[entityName] = state;
        }

        updater(state);
        SaveXpWindowStates();
    }

    private void ResetAllXpWindowPreferences()
    {
        void Apply()
        {
            try
            {
                var configPath = PersistentStorageHelper.GetAppDataPath(XpWindowStateFileName);
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }

                var legacyPath = PersistentStorageHelper.GetLegacyPath(XpWindowStateFileName);
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("KikimeterWindow", $"Impossible de supprimer la configuration des fenêtres XP: {ex.Message}");
            }

            _xpWindowStates.Clear();

            var ordered = _xpWindows.ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                var (entityName, window) = ordered[index];
                ResetXpWindowVisual(window, index);

                UpdateXpWindowState(entityName, state =>
                {
                    state.IsVisible = window.IsVisible;
                    state.Left = window.Left;
                    state.Top = window.Top;
                    state.Width = window.Width;
                    state.Height = window.Height;
                    state.ProgressColor = window.ViewModel.ProgressColorHex;
                });
            }

            SaveXpWindowStates();
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke((Action)Apply);
        }
        else
        {
            Apply();
        }
    }

    private void ResetXpWindowVisual(XpProgressWindow window, int index)
    {
        window.ViewModel.ResetColorToTheme();
        window.Width = Math.Max(window.MinWidth, XpWindowDefaultWidth);
        window.Height = Math.Max(window.MinHeight, XpWindowDefaultHeight);

        double baseLeft = Left + Width + 20;
        double baseTop = Top + 20 + index * (window.Height + 12);

        if (double.IsNaN(baseLeft) || double.IsInfinity(baseLeft))
        {
            baseLeft = 80 + index * 30;
        }

        if (double.IsNaN(baseTop) || double.IsInfinity(baseTop))
        {
            baseTop = 80 + index * (window.Height + 12);
        }

        window.Left = baseLeft;
        window.Top = baseTop;
    }

    private void RestoreXpWindows()
    {
        foreach (var kvp in _xpWindowStates)
        {
            if (kvp.Value.IsVisible)
            {
                ShowXpWindow(kvp.Key);
            }
        }
    }

    private static XpTrackerEntry CreateEmptyXpEntry(string entityName) =>
        new XpTrackerEntry
        {
            EntityName = entityName,
            ExperienceToNextLevel = null,
            TotalExperienceGained = 0,
            EventCount = 0,
            LastUpdate = DateTime.Now
        };

    private void ResetXpTracking(string entityName, XpProgressWindow? windowContext = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return;

        void Apply()
        {
            _xpCoordinator?.Reset(entityName);
            _lastXpEvents.Remove(entityName);

            var emptyEntry = CreateEmptyXpEntry(entityName);

            if (windowContext != null)
            {
                windowContext.ViewModel.ApplyEntry(emptyEntry, null);
                windowContext.Show();
            }
            else if (_xpWindows.TryGetValue(entityName, out var existingWindow))
            {
                existingWindow.ViewModel.ApplyEntry(emptyEntry, null);
                if (_xpWindowStates.TryGetValue(entityName, out var state) && state.IsVisible)
                {
                    existingWindow.Show();
                }
            }
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke((Action)Apply);
        }
        else
        {
            Apply();
        }
    }

    public void ResetAllXpTracking()
    {
        void Apply()
        {
            _xpCoordinator?.Reset();
            _lastXpEvents.Clear();

            foreach (var kvp in _xpWindows)
            {
                var entityName = kvp.Key;
                var window = kvp.Value;

                window.ViewModel.ApplyEntry(CreateEmptyXpEntry(entityName), null);

                if (_xpWindowStates.TryGetValue(entityName, out var state) && state.IsVisible)
                {
                    window.Show();
                }
            }

            SaveXpWindowStates();
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke((Action)Apply);
        }
        else
        {
            Apply();
        }
    }

    private void ToggleXpWindow(string entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return;

        try
        {
            if (_xpWindowStates.TryGetValue(entityName, out var state) && state.IsVisible)
            {
                HideXpWindow(entityName);
            }
            else
            {
                ShowXpWindow(entityName);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors du toggle de la fenêtre XP pour '{entityName}': {ex}");
        }
    }

    private void PlayerContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        if (menu.DataContext is not PlayerStats player)
            return;

        var isVisible = _xpWindowStates.TryGetValue(player.Name, out var state) && state.IsVisible;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            switch (item.Tag)
            {
                case "ShowXp":
                    item.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case "HideXp":
                    item.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        }
    }

    private void ShowXpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetPlayerFromMenuItem(sender) is { } player)
        {
            ShowXpWindow(player.Name);
        }
    }

    private void HideXpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetPlayerFromMenuItem(sender) is { } player)
        {
            HideXpWindow(player.Name);
        }
    }

    private void ResetXpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetPlayerFromMenuItem(sender) is { } player)
        {
            ResetXpTracking(player.Name);
        }
    }

    private static PlayerStats? GetPlayerFromMenuItem(object sender)
    {
        return sender switch
        {
            FrameworkElement element when element.DataContext is PlayerStats player => player,
            _ => null
        };
    }

    private void ClassIconBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement element && element.DataContext is PlayerStats player)
            {
                ToggleXpWindow(player.Name);
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors du clic sur l'icône de classe: {ex}");
        }
    }

    private void OnCombatStarted(object sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Signaler qu'un nouveau combat commence (le dégel sera fait dans InitializePlayersForNewCombat)
            _isNewCombat = true;
            Logger.Info("KikimeterWindow", "Nouveau combat détecté – initialisation complète des joueurs");
            
            // Fermer toutes les fenêtres individuelles du combat précédent
            foreach (var window in _playerWindows.Values.ToList())
            {
                try
                {
                    window.Close();
                }
                catch { }
            }
            _playerWindows.Clear();
            
            // Fermer la fenêtre indicateur si elle existe
            try
            {
                _indicatorWindow?.Close();
                _indicatorWindow = null;
            }
            catch { }
            
            // Réinitialiser les statistiques des joueurs existants
            foreach (var player in _playersCollection)
            {
                player.DamageDealt = 0;
                player.DamageTaken = 0;
                player.HealingDone = 0;
                player.ShieldGiven = 0;
                player.DamageBySummon = 0;
                player.DamageByAOE = 0;
                player.NumberOfTurns = 0;
                player.ResetTurnDamage();
            }
            
            // Réinitialiser le flag du premier tour complet
            _firstTurnComplete = false;
            
            // Réinitialiser les compteurs de kikis
            _playerKikis.Clear();
            
            // Mettre à jour LootWindow pour réinitialiser les joueurs du combat
            UpdateLootWindowCombatPlayers();
            
            // Réinitialiser les valeurs max affichées et cibles (progress bars)
            _displayedMaxDamage = 10000;
            _displayedMaxDamageTaken = 10000;
            _displayedMaxHealing = 10000;
            _displayedMaxShield = 10000;
            _maxDamage = 10000;
            _maxDamageTaken = 10000;
            _maxHealing = 10000;
            _maxShield = 10000;
            
            // Mettre à jour le statut du combat
            if (CombatStatusText != null)
            {
                CombatStatusText.Text = "Combat actif";
            }
            
            // Réafficher la fenêtre principale si elle était cachée
            if (_isIndividualMode && Visibility == Visibility.Hidden)
            {
                TryAutoShow();
            }
            
            Logger.Info("KikimeterWindow", "Nouveau combat - Toutes les stats réinitialisées");
        });
    }
    
    /// <summary>
    /// Met à jour LootWindow avec les joueurs du combat actuel
    /// IMPORTANT: Cette méthode est UNIDIRECTIONNELLE (Kikimeter → Loot)
    /// Le loot ne doit JAMAIS être une source de vérité pour la présence des joueurs
    /// </summary>
    private void UpdateLootWindowCombatPlayers()
    {
        // Ne pas mettre à jour pendant un reset ou si non initialisé
        if (_isResetInProgress || !_isInitialized)
        {
            return;
        }

        try
        {
            var lootWindow = System.Windows.Application.Current.Windows
                .OfType<LootWindow>()
                .FirstOrDefault();

            if (lootWindow == null)
            {
                return;
            }

            // UNIQUEMENT envoyer les joueurs du Kikimeter vers LootWindow
            // Le loot utilise ces informations pour afficher les loots, mais ne crée jamais de joueurs
            // Récupérer depuis la collection centrale (même si figé après combat, on envoie quand même)
            var players = _playersCollection
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (players.Count > 0)
            {
                // Envoi unidirectionnel : Kikimeter → Loot (pour affichage des loots uniquement)
                lootWindow.RegisterCombatPlayers(players);
                Logger.Debug("KikimeterWindow", $"Joueurs envoyés à LootWindow: {players.Count} joueurs");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdateLootWindowCombatPlayers: {ex.Message}");
        }
    }

    private void ResyncPlayersFromParser()
    {
        try
        {
            if (_logWatcher?.Parser == null)
            {
                return;
            }

            var parserPlayers = _logWatcher.Parser.PlayerStats.Values
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .ToList();

            foreach (var parserPlayer in parserPlayers)
            {
                var existing = _playersCollection
                    .FirstOrDefault(p => string.Equals(p.Name, parserPlayer.Name, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    _playersCollection.Add(parserPlayer);
                    parserPlayer.PropertyChanged += Player_PropertyChanged;
                }
                else if (!ReferenceEquals(existing, parserPlayer))
                {
                    existing.PropertyChanged -= Player_PropertyChanged;
                    var index = _playersCollection.IndexOf(existing);
                    _playersCollection[index] = parserPlayer;
                    parserPlayer.PropertyChanged += Player_PropertyChanged;
                }
            }
        }
        catch (Exception resyncEx)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors de la resynchronisation des joueurs: {resyncEx.Message}");
        }
    }

    /// <summary>
    /// Initialise complètement les joueurs pour un nouveau combat
    /// Reset du Kikimeter (progress bars et ordre de tour)
    /// Ajout complet des joueurs présents (JSON ou LogParser)
    /// Activation du suivi dynamique pour le combat
    /// </summary>
    private void InitializePlayersForNewCombat()
    {
        if (_playerDataProvider == null || _playerManagementService == null)
            return;

        try
        {
            Logger.Info("KikimeterWindow", "Début nouveau combat – initialisation joueurs");
            
            // DÉGELER le Kikimeter pour le nouveau combat (même si figé après le combat précédent)
            _freezeAfterCombat = false;

            // Récupérer tous les joueurs actuellement en combat depuis le provider
            var currentPlayerData = _playerDataProvider.GetCurrentPlayers();
            var playersInCombat = _playerDataProvider.GetPlayersInCombat();

            Logger.Debug("KikimeterWindow", $"Synchronisation initiale: {currentPlayerData.Count} joueurs dans JSON, {playersInCombat.Count} en combat");

            // ÉTAPE 1: Réinitialiser les stats de tous les joueurs existants
            foreach (var player in _playersCollection)
            {
                player.DamageDealt = 0;
                player.DamageTaken = 0;
                player.HealingDone = 0;
                player.ShieldGiven = 0;
                player.DamageBySummon = 0;
                player.DamageByAOE = 0;
                player.NumberOfTurns = 0;
                player.ResetTurnDamage();
            }

            // ÉTAPE 2: Synchroniser TOUS les joueurs actifs du combat (JSON + LogParser)
            // IMPORTANT: Ne jamais filtrer sur IsMainCharacter seul - afficher tous les joueurs actifs
            var playersToKeep = new HashSet<PlayerStats>();
            var now = DateTime.Now;
            
            // Étape 2a: Ajouter/mettre à jour tous les joueurs actifs depuis le JSON
            foreach (var playerData in currentPlayerData.Values)
            {
                // Inclure tous les joueurs actifs (pas seulement IsMainCharacter)
                if (!playerData.IsActive)
                    continue;

                // Chercher un joueur existant par nom ou ID
                var existing = _playersCollection.FirstOrDefault(p =>
                    string.Equals(p.Name, playerData.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.PlayerId.ToString(), playerData.Id, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Mettre à jour le joueur existant avec les données du provider
                    _playerManagementService.UpdatePlayerFromJson(existing, playerData);
                    existing.LastSeenInCombat = now;
                    existing.IsActive = true;
                    playersToKeep.Add(existing);
                    Logger.Debug("KikimeterWindow", $"Joueur existant mis à jour pour nouveau combat: {playerData.Name}");
                }
                else
                {
                    // Créer un nouveau joueur depuis les données JSON
                    var newPlayer = new PlayerStats
                    {
                        Name = playerData.Name,
                        PlayerId = long.TryParse(playerData.Id, out var id) ? id : 0,
                        IsInGroup = playerData.IsInGroup,
                        IsMainCharacter = playerData.IsMainCharacter,
                        IsActive = playerData.IsActive,
                        LastSeenInCombat = now
                    };

                    _playersCollection.Add(newPlayer);
                    newPlayer.PropertyChanged += Player_PropertyChanged;
                    playersToKeep.Add(newPlayer);
                    Logger.Info("KikimeterWindow", $"Joueur ajouté au Kikimeter : {playerData.Name}");
                }
            }
            
            // Étape 2b: S'assurer que TOUS les joueurs actifs en combat sont présents
            // (même s'ils ne sont pas encore dans currentPlayerData mais détectés par LogParser)
            foreach (var playerNameOrId in playersInCombat)
            {
                // Vérifier si ce joueur est déjà dans la collection
                var existing = _playersCollection.FirstOrDefault(p =>
                    string.Equals(p.Name, playerNameOrId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.PlayerId.ToString(), playerNameOrId, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    // Joueur actif en combat mais pas encore dans la collection
                    // Essayer de le trouver dans le LogParser si disponible
                    if (_logWatcher?.Parser != null)
                    {
                        var parserStats = _logWatcher.Parser.PlayerStats.Values
                            .FirstOrDefault(p => 
                                string.Equals(p.Name, playerNameOrId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(p.PlayerId.ToString(), playerNameOrId, StringComparison.OrdinalIgnoreCase));

                        if (parserStats != null)
                        {
                            // Créer un nouveau joueur depuis le LogParser
                            var newPlayer = new PlayerStats
                            {
                                Name = parserStats.Name,
                                PlayerId = parserStats.PlayerId,
                                Breed = parserStats.Breed,
                                ClassName = parserStats.ClassName,
                                IsActive = true,
                                IsInGroup = parserStats.IsInGroup,
                                IsMainCharacter = parserStats.IsMainCharacter,
                                LastSeenInCombat = now
                            };

                            _playersCollection.Add(newPlayer);
                            newPlayer.PropertyChanged += Player_PropertyChanged;
                            playersToKeep.Add(newPlayer);
                            Logger.Info("KikimeterWindow", $"Joueur ajouté au Kikimeter : {newPlayer.Name} (depuis LogParser)");
                        }
                    }
                }
                else
                {
                    // Mettre à jour le joueur existant pour s'assurer qu'il est actif
                    existing.IsActive = true;
                    existing.LastSeenInCombat = now;
                    playersToKeep.Add(existing);
                }
            }

            // ÉTAPE 4: Retirer les joueurs qui ne sont plus dans le combat
            // MAIS conserver le personnage principal et les joueurs du groupe
            var playersToRemove = new List<PlayerStats>();
            foreach (var player in _playersCollection.ToList())
            {
                // Ne jamais retirer le personnage principal
                if (player.IsMainCharacter)
                {
                    playersToKeep.Add(player);
                    continue;
                }

                // Ne jamais retirer les joueurs du groupe
                if (player.IsInGroup)
                {
                    playersToKeep.Add(player);
                    continue;
                }

                // Retirer uniquement si le joueur n'est pas dans la liste à conserver
                if (!playersToKeep.Contains(player))
                {
                    playersToRemove.Add(player);
                }
            }

            // Retirer les joueurs identifiés
            foreach (var player in playersToRemove)
            {
                try
                {
                    player.PropertyChanged -= Player_PropertyChanged;
                    _playersCollection.Remove(player);
                    Logger.Debug("KikimeterWindow", $"Joueur retiré pour nouveau combat: {player.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Error("KikimeterWindow", $"Erreur lors de la suppression de '{player.Name}' pour nouveau combat: {ex.Message}");
                }
            }

            // ÉTAPE 5: Sélection automatique du personnage principal si aucun n'est défini
            if (_playerManagementService != null)
            {
                var hasMainCharacter = _playersCollection.Any(p => p.IsMainCharacter);
                if (!hasMainCharacter && _playersCollection.Count > 0)
                {
                    // Sélectionner automatiquement le premier joueur actif du groupe
                    var firstGroupPlayer = _playersCollection
                        .Where(p => p.IsActive && p.IsInGroup)
                        .OrderBy(p => p.LastSeenInCombat)
                        .FirstOrDefault();
                        
                    if (firstGroupPlayer != null)
                    {
                        _playerManagementService.SetMainCharacter(firstGroupPlayer.Name);
                        Logger.Info("KikimeterWindow", $"Personnage principal : {firstGroupPlayer.Name}");
                    }
                    else
                    {
                        // Si aucun joueur du groupe, sélectionner le premier joueur actif
                        var firstActivePlayer = _playersCollection
                            .Where(p => p.IsActive)
                            .OrderBy(p => p.LastSeenInCombat)
                            .FirstOrDefault();
                            
                        if (firstActivePlayer != null)
                        {
                            _playerManagementService.SetMainCharacter(firstActivePlayer.Name);
                            Logger.Info("KikimeterWindow", $"Personnage principal : {firstActivePlayer.Name}");
                        }
                    }
                }
            }

            // ÉTAPE 6: Réinitialiser les progress bars (déjà fait dans OnCombatStarted, mais on s'assure)
            _displayedMaxDamage = 10000;
            _displayedMaxDamageTaken = 10000;
            _displayedMaxHealing = 10000;
            _displayedMaxShield = 10000;

            Logger.Info("KikimeterWindow", $"Initialisation terminée: {_playersCollection.Count} joueurs prêts pour le nouveau combat - Suivi dynamique activé");
            
            // Notifier LootWindow des joueurs du nouveau combat (via l'event PlayersChanged)
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors de la synchronisation initiale pour nouveau combat: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Appelé lorsque la liste des joueurs change dans PlayerManagementService
    /// Notifie automatiquement LootWindow et SettingsWindow
    /// </summary>
    private void OnPlayersChangedFromService(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                // Notifier LootWindow des joueurs actuels
                UpdateLootWindowCombatPlayers();
                
                Logger.Debug("KikimeterWindow", $"PlayersChanged event reçu - {_playersCollection.Count} joueurs");
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Erreur lors de la notification des changements de joueurs: {ex.Message}");
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
    
    /// <summary>
    /// Appelé lorsque le personnage principal change dans PlayerManagementService
    /// </summary>
    private void OnMainCharacterChangedFromService(object? sender, PlayerStats player)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Logger.Info("KikimeterWindow", $"Personnage principal changé : {player.Name}");
                // La propriété IsMainCharacter est déjà mise à jour dans le PlayerStats
                // L'UI se mettra à jour automatiquement via le binding
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Erreur lors du changement de personnage principal: {ex.Message}");
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Synchronise les joueurs pendant le combat avec une mise à jour en diff
    /// Ajoute les nouveaux joueurs détectés, retire uniquement les absents (sauf personnage principal)
    /// Ne vide JAMAIS la collection entièrement pendant le combat
    /// </summary>
    private void SyncPlayersDuringCombat()
    {
        if (_playerDataProvider == null || _playerManagementService == null)
            return;

        // Ne pas synchroniser si le Kikimeter est figé après le combat
        // MAIS permettre la synchronisation si c'est un nouveau combat (initialisation)
        if (_freezeAfterCombat && !_isNewCombat)
        {
            Logger.Debug("KikimeterWindow", "Synchronisation suspendue car le Kikimeter est figé après le combat");
            return;
        }

        try
        {
            // Récupérer les joueurs actuellement en combat depuis le provider
            var currentPlayerData = _playerDataProvider.GetCurrentPlayers();
            var playersInCombat = _playerDataProvider.GetPlayersInCombat();

            Logger.Debug("KikimeterWindow", $"Synchronisation pendant combat: {currentPlayerData.Count} joueurs dans JSON, {playersInCombat.Count} en combat");

            // ÉTAPE 1: Ajouter TOUS les nouveaux joueurs actifs détectés (JSON + LogParser)
            // IMPORTANT: Ne jamais filtrer sur IsMainCharacter seul - afficher tous les joueurs actifs
            var now = DateTime.Now;
            
            // Étape 1a: Ajouter/mettre à jour les joueurs actifs depuis le JSON
            foreach (var playerData in currentPlayerData.Values)
            {
                // Vérifier si le joueur existe déjà dans la collection
                var existing = _playersCollection.FirstOrDefault(p =>
                    string.Equals(p.Name, playerData.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.PlayerId.ToString(), playerData.Id, StringComparison.OrdinalIgnoreCase));

                if (existing == null && playerData.IsActive)
                {
                    // Créer un nouveau joueur depuis les données JSON
                    var newPlayer = new PlayerStats
                    {
                        Name = playerData.Name,
                        PlayerId = long.TryParse(playerData.Id, out var id) ? id : 0,
                        IsInGroup = playerData.IsInGroup,
                        IsMainCharacter = playerData.IsMainCharacter,
                        IsActive = playerData.IsActive,
                        LastSeenInCombat = playerData.LastSeenInCombat != DateTime.MinValue ? playerData.LastSeenInCombat : now
                    };

                    _playersCollection.Add(newPlayer);
                    newPlayer.PropertyChanged += Player_PropertyChanged;
                    Logger.Info("KikimeterWindow", $"Joueur ajouté au Kikimeter : {playerData.Name}");
                    
                    // Sélection automatique du personnage principal si aucun n'est défini
                    if (_playerManagementService != null && !_playersCollection.Any(p => p.IsMainCharacter))
                    {
                        _playerManagementService.SetMainCharacter(newPlayer.Name);
                        Logger.Info("KikimeterWindow", $"Personnage principal : {newPlayer.Name}");
                    }
                }
                else if (existing != null)
                {
                    // Mettre à jour les propriétés du joueur existant
                    _playerManagementService.UpdatePlayerFromJson(existing, playerData);
                    
                    // Si le joueur est dans le combat actuel, mettre à jour LastSeenInCombat
                    if (playersInCombat.Contains(playerData.Name) || playersInCombat.Contains(playerData.Id))
                    {
                        existing.LastSeenInCombat = now;
                        existing.IsActive = true;
                    }
                }
            }
            
            // Étape 1b: S'assurer que TOUS les joueurs actifs en combat sont présents
            // (même s'ils ne sont pas encore dans currentPlayerData mais détectés par LogParser)
            foreach (var playerNameOrId in playersInCombat)
            {
                // Vérifier si ce joueur est déjà dans la collection
                var existing = _playersCollection.FirstOrDefault(p =>
                    string.Equals(p.Name, playerNameOrId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.PlayerId.ToString(), playerNameOrId, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    // Joueur actif en combat mais pas encore dans la collection
                    // Essayer de le trouver dans le LogParser si disponible
                    if (_logWatcher?.Parser != null)
                    {
                        var parserStats = _logWatcher.Parser.PlayerStats.Values
                            .FirstOrDefault(p => 
                                string.Equals(p.Name, playerNameOrId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(p.PlayerId.ToString(), playerNameOrId, StringComparison.OrdinalIgnoreCase));

                        if (parserStats != null)
                        {
                            // Créer un nouveau joueur depuis le LogParser
                            var newPlayer = new PlayerStats
                            {
                                Name = parserStats.Name,
                                PlayerId = parserStats.PlayerId,
                                Breed = parserStats.Breed,
                                ClassName = parserStats.ClassName,
                                IsActive = true,
                                IsInGroup = parserStats.IsInGroup,
                                IsMainCharacter = parserStats.IsMainCharacter,
                                LastSeenInCombat = now
                            };

                            _playersCollection.Add(newPlayer);
                            newPlayer.PropertyChanged += Player_PropertyChanged;
                            Logger.Info("KikimeterWindow", $"Joueur ajouté au Kikimeter : {newPlayer.Name} (depuis LogParser)");
                            
                            // Sélection automatique du personnage principal si aucun n'est défini
                            if (_playerManagementService != null && !_playersCollection.Any(p => p.IsMainCharacter))
                            {
                                _playerManagementService.SetMainCharacter(newPlayer.Name);
                                Logger.Info("KikimeterWindow", $"Personnage principal : {newPlayer.Name}");
                            }
                        }
                    }
                }
                else
                {
                    // Mettre à jour le joueur existant pour s'assurer qu'il est actif
                    existing.IsActive = true;
                    existing.LastSeenInCombat = now;
                }
            }

            // ÉTAPE 2: Retirer uniquement les joueurs absents (sauf personnage principal et joueurs du groupe)
            // Ne jamais vider la collection entièrement pendant le combat
            var playersToRemove = new List<PlayerStats>();
            foreach (var player in _playersCollection.ToList())
            {
                // Ne jamais retirer le personnage principal
                if (player.IsMainCharacter)
                {
                    continue;
                }

                // Ne jamais retirer les joueurs du groupe
                if (player.IsInGroup)
                {
                    continue;
                }

                // Vérifier si le joueur est toujours dans les données JSON ou en combat
                var existsInJson = currentPlayerData.Values.Any(p =>
                    string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Id, player.PlayerId.ToString(), StringComparison.OrdinalIgnoreCase));

                var isInCombat = playersInCombat.Contains(player.Name) || playersInCombat.Contains(player.PlayerId.ToString());

                // Retirer uniquement si le joueur n'est ni dans le JSON ni en combat
                if (!existsInJson && !isInCombat)
                {
                    playersToRemove.Add(player);
                }
            }

            // Retirer les joueurs identifiés
            foreach (var player in playersToRemove)
            {
                try
                {
                    player.PropertyChanged -= Player_PropertyChanged;
                    _playersCollection.Remove(player);
                    Logger.Info("KikimeterWindow", $"Joueur retiré du Kikimeter : {player.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Error("KikimeterWindow", $"Erreur lors de la suppression de '{player.Name}' pendant combat: {ex.Message}");
                }
            }

            Logger.Debug("KikimeterWindow", $"Synchronisation pendant combat terminée: {_playersCollection.Count} joueurs affichés");
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur lors de la synchronisation pendant combat: {ex.Message}");
        }
    }
    
    private void OnCombatEnded(object sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Ne pas nettoyer pendant un reset serveur
            if (_isResetInProgress)
            {
                Logger.Debug("KikimeterWindow", "OnCombatEnded ignoré car reset en cours");
                return;
            }

            // Vérifier que le combat est bien terminé avant de nettoyer
            if (_playerDataProvider != null && _playerDataProvider.IsCombatActive)
            {
                Logger.Debug("KikimeterWindow", "OnCombatEnded: Combat toujours actif selon le provider, nettoyage différé");
                return;
            }

            // FIGER le Kikimeter après le combat pour permettre l'analyse des stats
            _freezeAfterCombat = true;
            _isNewCombat = false; // Le combat est terminé, plus besoin d'initialisation
            Logger.Info("KikimeterWindow", "Kikimeter figé après combat");

            if (CombatStatusText != null)
            {
                CombatStatusText.Text = "Combat terminé - Statistiques finales";
            }
            
            // Ne PAS nettoyer automatiquement les joueurs après la fin du combat si figé
            // Le Kikimeter reste figé avec les stats du combat précédent
            // Le nettoyage sera effectué au début du prochain combat
            if (_playerManagementService != null && _isInitialized)
            {
                try
                {
                    Logger.Info("KikimeterWindow", $"Kikimeter figé après combat ({_playersCollection.Count} joueurs) - Nettoyage différé au prochain combat");
                    // Ne pas appeler CleanupPlayersAfterCombat car le Kikimeter est figé
                    // Le nettoyage sera effectué au début du prochain combat dans OnCombatStarted
                    
                    // Mettre à jour la fenêtre de loot avec les joueurs restants (unidirectionnel)
                    UpdateLootWindowCombatPlayers();
                    
                    // Mettre à jour la hauteur de la fenêtre si nécessaire
                    UpdateWindowHeight();
                    
                    Logger.Info("KikimeterWindow", $"Kikimeter figé - {_playersCollection.Count} joueurs conservés pour analyse");
                }
                catch (Exception ex)
                {
                    Logger.Error("KikimeterWindow", $"Erreur lors du nettoyage automatique des joueurs: {ex.Message}");
                }
            }
        });
    }
    
    private void Player_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Mise à jour des valeurs max si nécessaire
        
        // Si le premier tour complet est terminé, ne plus trier (le tri automatique au premier tour a la priorité sur l'ordre manuel)
        if (_firstTurnComplete)
            return;
        
        // Si la propriété NumberOfTurns change, vérifier si c'est le premier tour d'un joueur
        if (e.PropertyName == nameof(PlayerStats.NumberOfTurns) && sender is PlayerStats player)
        {
            // Si le joueur vient de jouer son premier tour (NumberOfTurns passe de 0 à 1)
            if (player.NumberOfTurns == 1)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Compter combien de joueurs ont déjà joué au moins une fois
                        int playersWhoPlayedCount = _playersCollection.Count(p => p.NumberOfTurns >= 1);
                        
                        // Trouver la position actuelle du joueur
                        int currentIndex = _playersCollection.IndexOf(player);
                        
                        // Si le joueur n'est pas déjà à la bonne position, le déplacer
                        // La position doit être juste après le dernier joueur qui a déjà joué
                        int targetIndex = playersWhoPlayedCount - 1; // -1 car on compte à partir de 0
                        
                        if (currentIndex != targetIndex && targetIndex >= 0)
                        {
                            _playersCollection.Move(currentIndex, targetIndex);
                            Logger.Info("KikimeterWindow", $"Joueur {player.Name} déplacé à la position {targetIndex} (premier tour)");
                        }
                        
                        // Vérifier si tous les joueurs ont joué au moins une fois
                        bool allPlayersPlayed = _playersCollection.All(p => p.NumberOfTurns >= 1);
                        if (allPlayersPlayed && !_firstTurnComplete)
                        {
                            _firstTurnComplete = true;
                            Logger.Info("KikimeterWindow", "Premier tour complet terminé - ordre des joueurs figé");
                        }
                        
                        UpdateFirstPlayerFlag();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("KikimeterWindow", $"Erreur lors du tri automatique au premier tour: {ex.Message}");
                    }
                }), DispatcherPriority.Normal);
            }
        }
    }
    
    private DateTime _lastArrowUpdateTime = DateTime.MinValue;
    
    private void UpdateTimer_Tick(object sender, EventArgs e)
    {
        try
        {
            if (_logWatcher != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        _logWatcher.ManualRead();
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error("KikimeterWindow", $"Erreur lors de ManualRead: {ex2.Message}");
                    }
                });
            }
            
            // Mise à jour en diff pendant le combat (ajout/retrait uniquement des joueurs nécessaires)
            // Ne jamais vider la collection entièrement pendant le combat
            var now = DateTime.Now;
            if (_playerManagementService != null && 
                _playerDataProvider != null &&
                _isInitialized &&
                !_isResetInProgress)
            {
                var isCombatActive = _playerDataProvider.IsCombatActive;

                // Filet de sécurité:
                // si un combat est actif mais que le Kikimeter est resté figé (événement CombatStarted manqué),
                // on force la reprise pour réinitialiser correctement les compteurs.
                if (isCombatActive && _freezeAfterCombat && !_isNewCombat)
                {
                    Logger.Warning("KikimeterWindow", "Combat actif détecté alors que le Kikimeter est figé: reprise forcée du suivi.");
                    _isNewCombat = true;
                    _freezeAfterCombat = false;
                }
                
                // NOUVEAU COMBAT : Initialisation complète (une seule fois)
                // Le flag _freezeAfterCombat est ignoré pour permettre l'initialisation
                if (_isNewCombat && isCombatActive)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            InitializePlayersForNewCombat();
                            _isNewCombat = false; // Après initialisation, passer en mode diff normal
                        }
                        catch (Exception initEx)
                        {
                            Logger.Error("KikimeterWindow", $"Erreur lors de l'initialisation pour nouveau combat: {initEx.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else if (isCombatActive && !_isNewCombat && !_freezeAfterCombat)
                {
                    // PENDANT LE COMBAT : Mise à jour en diff uniquement
                    // Ajouter les nouveaux joueurs détectés, retirer uniquement les absents (sauf personnage principal)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            Logger.Debug("KikimeterWindow", "Mise à jour en diff pendant combat");
                            SyncPlayersDuringCombat();
                        }
                        catch (Exception syncEx)
                        {
                            Logger.Error("KikimeterWindow", $"Erreur lors de la synchronisation pendant le combat: {syncEx.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else if (!isCombatActive && !_freezeAfterCombat && (now - _lastPlayerSyncTime).TotalSeconds >= PlayerSyncIntervalSeconds)
                {
                    // HORS COMBAT : Synchronisation périodique complète (toutes les 5 secondes)
                    // Seulement si le Kikimeter n'est pas figé
                    _lastPlayerSyncTime = now;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            Logger.Debug("KikimeterWindow", "Synchronisation périodique hors combat");
                            _playerManagementService.SyncPlayersWithJson(_playersCollection, skipIfResetting: true);
                        }
                        catch (Exception syncEx)
                        {
                            Logger.Error("KikimeterWindow", $"Erreur lors de la synchronisation périodique: {syncEx.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            
            // Log de debug si figé après combat
            if (_freezeAfterCombat && !_isNewCombat)
            {
                Logger.Debug("KikimeterWindow", "Kikimeter figé après combat");
            }
            
            // Mettre à jour la position de la flèche collapse périodiquement (toutes les 500ms)
            // pour s'adapter aux changements de taille de la barre (ex: DPT qui change)
            // La flèche est maintenant en position fixe, plus besoin de mise à jour périodique
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans UpdateTimer_Tick: {ex.Message}");
        }
    }

    private void ParseLogLine(string line)
    {
        // Patterns pour détecter les kikis dans les logs Wakfu
        // Format typique : [timestamp] Kiki gagné par [PlayerName] ou variations
        var kikiPatterns = new[]
        {
            @"Kiki.*?par\s+(\w+)",
            @"Kiki.*?gagné.*?par\s+(\w+)",
            @"Kiki.*?obtenu.*?par\s+(\w+)",
            @"(\w+).*?a.*?gagné.*?un.*?Kiki",
            @"(\w+).*?obtient.*?un.*?Kiki",
            @"Kiki.*?(\w+)",
            @"\[.*?\]\s*(\w+).*?Kiki"
        };
        
        foreach (var pattern in kikiPatterns)
        {
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var playerName = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(playerName) && playerName.Length > 0 && playerName.Length < 50)
                {
                    if (!_playerKikis.ContainsKey(playerName))
                    {
                        _playerKikis[playerName] = 0;
                    }
                    _playerKikis[playerName]++;
                    
                    UpdatePlayerKiki(playerName, _playerKikis[playerName]);
                    Logger.Info("KikimeterWindow", $"Kiki détecté pour {playerName}: {_playerKikis[playerName]}");
                    break; // Arrêter après le premier match
                }
            }
        }
    }

    private void UpdatePlayerKiki(string playerName, int count)
    {
        // Cette méthode est maintenant remplacée par OnPlayerAdded du parser
        // Gardée pour compatibilité avec ParseLogLine si nécessaire
        _playerKikis[playerName] = count;
    }
    
    private void OnLogLineProcessed(object? sender, string line)
    {
        Dispatcher.Invoke(() => ParseLogLine(line));
    }
    
    private void OnWatcherError(object? sender, string error)
    {
        Logger.Error("KikimeterWindow", $"Erreur du watcher: {error}");
    }
    
    private void OnLogFileNotFound(object? sender, EventArgs e)
    {
        Logger.Warning("KikimeterWindow", $"Fichier de log non trouvé: '{_logPath}'. Veuillez vérifier le chemin dans les paramètres.");
    }

    private void UpdateMainWindow()
    {
        // La collection est mise à jour automatiquement via le parser
        // Plus besoin de cette méthode si on utilise le parser complet
    }

    private void LoadPlayerWindowPosition(string playerName, PlayerWindow window)
    {
        try
        {
            var positions = PersistentStorageHelper.LoadJsonWithFallback<PlayerWindowPositions>("player_window_positions.json");
            if (positions != null && positions.ContainsKey(playerName))
            {
                var pos = positions[playerName];
                window.Left = pos.Left;
                window.Top = pos.Top;
                window.Width = pos.Width;
                window.Height = pos.Height;
            }
        }
        catch { }
    }

    public void StopWatching()
    {
        _logWatcher?.StopWatching();
        _updateTimer?.Stop();
        if (_xpCoordinator != null)
        {
            _xpCoordinator.ExperienceGained -= OnExperienceGained;
            _xpCoordinator.Dispose();
            _xpCoordinator = null;
        }
    }

    private static void EnsureBindingDiagnostics()
    {
        if (_bindingDiagnosticsEnabled)
            return;

        lock (_bindingDiagnosticsLock)
        {
            if (_bindingDiagnosticsEnabled)
                return;

            try
            {
                var listener = new BindingLoggerTraceListener();
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
                PresentationTraceSources.DependencyPropertySource.Listeners.Add(listener);
                PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.All;
                PresentationTraceSources.DependencyPropertySource.Switch.Level = SourceLevels.All;
                Logger.Info("KikimeterWindow", "Diagnostics de binding WPF activés (niveau maximal).");
                _bindingDiagnosticsEnabled = true;
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Impossible d'activer les diagnostics de binding: {ex.Message}");
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveWindowPositions();
        // Sauvegarder toutes les fenêtres individuelles avant de les fermer
        foreach (var kvp in _playerWindows)
        {
            try
            {
                SavePlayerWindowPosition(kvp.Key, kvp.Value);
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Erreur sauvegarde fenêtre {kvp.Key} lors de la fermeture: {ex.Message}");
            }
        }
        StopWatching();
        foreach (var window in _playerWindows.Values)
        {
            window.Close();
        }
        base.OnClosed(e);
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Permettre le drag depuis toute la barre de titre
        if (e.ChangedButton == MouseButton.Left)
        {
            // Ne pas capturer si on clique sur un bouton ou checkbox
            if (e.OriginalSource is System.Windows.Controls.Button || 
                e.OriginalSource is System.Windows.Controls.CheckBox)
            {
                return;
            }
            
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            CaptureMouse();
        }
    }
    
    
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button)
        {
            return;
        }
        if (e.ChangedButton == MouseButton.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            CaptureMouse();
        }
    }
    
    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var offset = e.GetPosition(this) - _dragStartPoint;
            Left += offset.X;
            Top += offset.Y;
        }
    }
    
    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            ReturnFocusToGame();
        }
    }
    
    private bool _isMinimized = false;
    private double _savedHeight = 0;
    
    private System.Windows.Controls.Button? GetMinimizeButton()
    {
        return this.FindName("MinimizeButton") as System.Windows.Controls.Button;
    }
    
    private System.Windows.Controls.Grid? GetMainGrid()
    {
        return this.FindName("MainGrid") as System.Windows.Controls.Grid;
    }
    
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isMinimized)
            {
                // Restaurer la fenêtre : remettre la hauteur originale
                Height = _savedHeight;
                
                // Réafficher le contenu principal
                var mainGrid = GetMainGrid();
                if (mainGrid != null && mainGrid.RowDefinitions.Count > 1)
                {
                    mainGrid.RowDefinitions[1].Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                }
                
                _isMinimized = false;
                var minimizeBtn = GetMinimizeButton();
                if (minimizeBtn != null)
                {
                    minimizeBtn.Content = "─";
                }
                Logger.Debug("KikimeterWindow", "Fenêtre restaurée via bouton de réduction");
            }
            else
            {
                // Minimiser : réduire la fenêtre à juste la barre de titre
                // Sauvegarder la hauteur actuelle
                _savedHeight = Height;
                
                // Réduire la hauteur à juste la barre de titre (environ 40-50 pixels)
                // La hauteur minimale = hauteur de la barre de titre + bordure
                Height = 50; // Ajuster selon la hauteur réelle de la barre de titre
                
                // Cacher le contenu principal (Row 1)
                var mainGrid = GetMainGrid();
                if (mainGrid != null && mainGrid.RowDefinitions.Count > 1)
                {
                    mainGrid.RowDefinitions[1].Height = new System.Windows.GridLength(0);
                }
                
                _isMinimized = true;
                var minimizeBtn = GetMinimizeButton();
                if (minimizeBtn != null)
                {
                    minimizeBtn.Content = "□";
                }
                Logger.Debug("KikimeterWindow", "Fenêtre minimisée via bouton de réduction");
                ReturnFocusToGame();
            }
            
            // S'assurer que la fenêtre reste visible sur l'écran principal s'il n'y a qu'un écran
            EnsureWindowVisibleOnPrimaryScreenIfSingleMonitor();
            
            // Sauvegarder l'état après le changement
            SaveWindowPositions();
        }
        catch (Exception ex)
        {
            Logger.Error("KikimeterWindow", $"Erreur dans MinimizeButton_Click: {ex.Message}");
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideFromController(true);
        ReturnFocusToGame();
    }

    public bool UserRequestedHidden => _userRequestedHidden;

    public void HideFromController(bool userInitiated)
    {
        if (userInitiated)
        {
            _userRequestedHidden = true;
        }
        Hide();
    }

    public void ShowFromController(bool focusWindow, bool resetUserFlag = true)
    {
        if (resetUserFlag)
        {
            _userRequestedHidden = false;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (focusWindow)
        {
            try
            {
                Activate();
                Focus();
            }
            catch
            {
                // ignore focus errors
            }
        }
    }

    /// <summary>
    /// S'assure que la fenêtre du Kikimeter reste entièrement visible sur l'écran principal
    /// lorsque l'utilisateur n'a qu'un seul écran.
    /// Empêche que la fenêtre soit partiellement hors écran après un réduit/déployé.
    /// </summary>
    private void EnsureWindowVisibleOnPrimaryScreenIfSingleMonitor()
    {
        try
        {
            // Ne corriger que si un seul écran est présent
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens == null || screens.Length != 1)
            {
                return;
            }

            var screen = screens[0].WorkingArea;

            // Ajuster taille si elle dépasse l'écran
            if (Width > screen.Width)
            {
                Width = screen.Width;
            }
            if (Height > screen.Height)
            {
                Height = screen.Height;
            }

            // Recalculer la position pour que la fenêtre reste dans les bornes
            double newLeft = Left;
            double newTop = Top;

            if (newLeft < screen.Left)
                newLeft = screen.Left;
            if (newTop < screen.Top)
                newTop = screen.Top;
            if (newLeft + Width > screen.Right)
                newLeft = screen.Right - Width;
            if (newTop + Height > screen.Bottom)
                newTop = screen.Bottom - Height;

            Left = newLeft;
            Top = newTop;
        }
        catch (Exception ex)
        {
            Logger.Warning("KikimeterWindow", $"Erreur dans EnsureWindowVisibleOnPrimaryScreenIfSingleMonitor: {ex.Message}");
        }
    }

    private void TryAutoShow()
    {
        if (_userRequestedHidden)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }
    }
    
    private void ReturnFocusToGame()
    {
        if (_suspendFocusReturn)
        {
            return;
        }
        DispatcherTimer timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            try
            {
                var wakfuProcesses = System.Diagnostics.Process.GetProcessesByName("Wakfu")
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero)
                    .OrderByDescending(p => p.MainWindowTitle.Contains("Wakfu"))
                    .ToList();
                if (wakfuProcesses.Any())
                {
                    IntPtr hwnd = wakfuProcesses.First().MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        ShowWindow(hwnd, 9);
                        SetForegroundWindow(hwnd);
                    }
                }
                else
                {
                    string[] processNames = { "Wakfu.exe", "wakfu", "WAKFU" };
                    foreach (string name in processNames)
                    {
                        var processes = System.Diagnostics.Process.GetProcessesByName(name.Replace(".exe", ""))
                            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero);
                        if (processes.Any())
                        {
                            IntPtr hwnd2 = processes.First().MainWindowHandle;
                            if (hwnd2 != IntPtr.Zero)
                            {
                                ShowWindow(hwnd2, 9);
                                SetForegroundWindow(hwnd2);
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        };
        timer.Start();
    }
    
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private sealed class BindingLoggerTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Logger.Warning("Binding", message);
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Logger.Warning("Binding", message);
            }
        }
    }
}
