using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service centralisé de gestion des joueurs - SOURCE UNIQUE DE VÉRITÉ
/// Gère le nettoyage des joueurs après les combats, la gestion des groupes et des adversaires
/// Notifie automatiquement tous les composants (Kikimeter, Paramètres, LootWindow) des changements
/// </summary>
public class PlayerManagementService
{
    private const string LogCategory = "PlayerManagementService";
    private const int MaxGroupSize = 6;
    private const int CombatInactivityTimeoutSeconds = 30; // Délai avant de considérer un joueur comme inactif après la fin d'un combat

    private readonly IPlayerDataProvider _dataProvider;
    private DateTime _lastCombatEndTime = DateTime.MinValue;
    private string? _lastServerName;
    private bool _isResetting = false; // Flag pour éviter le nettoyage pendant un reset serveur
    
    // Collection centrale des joueurs (référence vers la collection de KikimeterWindow)
    private ObservableCollection<PlayerStats>? _playersCollection;

    /// <summary>
    /// Event déclenché lorsque la liste des joueurs change (ajout, retrait, mise à jour)
    /// Tous les composants (Kikimeter, Paramètres, LootWindow) doivent s'abonner à cet event
    /// </summary>
    public event EventHandler? PlayersChanged;

    /// <summary>
    /// Event déclenché lorsque le personnage principal change
    /// </summary>
    public event EventHandler<PlayerStats>? MainCharacterChanged;
    
    /// <summary>
    /// Définit la collection de joueurs à gérer (doit être la collection de KikimeterWindow)
    /// </summary>
    public void SetPlayersCollection(ObservableCollection<PlayerStats> playersCollection)
    {
        if (_playersCollection != null)
        {
            _playersCollection.CollectionChanged -= PlayersCollection_CollectionChanged;
        }
        
        _playersCollection = playersCollection ?? throw new ArgumentNullException(nameof(playersCollection));
        
        // S'abonner aux changements de la collection pour notifier les composants
        _playersCollection.CollectionChanged += PlayersCollection_CollectionChanged;
        
        Logger.Info(LogCategory, "Collection de joueurs définie dans PlayerManagementService");
        OnPlayersChanged();
    }
    
    private void PlayersCollection_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPlayersChanged();
    }

    public PlayerManagementService(IPlayerDataProvider dataProvider)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
    }
    
    /// <summary>
    /// Obtient la liste complète des joueurs (noms uniquement) pour SettingsWindow et autres composants
    /// </summary>
    public IEnumerable<string> GetCurrentPlayerNames()
    {
        if (_playersCollection == null)
            return Enumerable.Empty<string>();
            
        return _playersCollection
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    
    /// <summary>
    /// Obtient le personnage principal actuel
    /// </summary>
    public PlayerStats? GetMainCharacter()
    {
        return _playersCollection?.FirstOrDefault(p => p.IsMainCharacter);
    }
    
    /// <summary>
    /// Définit un joueur comme personnage principal
    /// </summary>
    public void SetMainCharacter(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || _playersCollection == null)
            return;
            
        var player = _playersCollection.FirstOrDefault(p => 
            string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase));
            
        if (player == null)
            return;
            
        // Désélectionner l'ancien personnage principal
        foreach (var p in _playersCollection.Where(p => p.IsMainCharacter))
        {
            p.IsMainCharacter = false;
        }
        
        // Définir le nouveau personnage principal
        player.IsMainCharacter = true;
        Logger.Info(LogCategory, $"Personnage principal : {playerName}");
        OnMainCharacterChanged(player);
        OnPlayersChanged();
    }
    
    /// <summary>
    /// Sélectionne automatiquement le premier joueur détecté comme personnage principal
    /// </summary>
    private void AutoSelectMainCharacter(ObservableCollection<PlayerStats> playersCollection)
    {
        // Vérifier si un personnage principal existe déjà
        if (playersCollection.Any(p => p.IsMainCharacter))
            return;
            
        // Sélectionner automatiquement le premier joueur actif du groupe
        var firstGroupPlayer = playersCollection
            .Where(p => p.IsActive && p.IsInGroup)
            .OrderBy(p => p.LastSeenInCombat)
            .FirstOrDefault();
            
        if (firstGroupPlayer != null)
        {
            firstGroupPlayer.IsMainCharacter = true;
            Logger.Info(LogCategory, $"Premier personnage automatiquement sélectionné comme principal : {firstGroupPlayer.Name}");
            OnMainCharacterChanged(firstGroupPlayer);
        }
        else
        {
            // Si aucun joueur du groupe, sélectionner le premier joueur actif
            var firstActivePlayer = playersCollection
                .Where(p => p.IsActive)
                .OrderBy(p => p.LastSeenInCombat)
                .FirstOrDefault();
                
            if (firstActivePlayer != null)
            {
                firstActivePlayer.IsMainCharacter = true;
                Logger.Info(LogCategory, $"Premier joueur actif automatiquement sélectionné comme principal : {firstActivePlayer.Name}");
                OnMainCharacterChanged(firstActivePlayer);
            }
        }
    }
    
    /// <summary>
    /// Notifie les composants que la liste des joueurs a changé
    /// </summary>
    private void OnPlayersChanged()
    {
        PlayersChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Notifie les composants que le personnage principal a changé
    /// </summary>
    private void OnMainCharacterChanged(PlayerStats player)
    {
        MainCharacterChanged?.Invoke(this, player);
    }

    /// <summary>
    /// Nettoie automatiquement les joueurs après la fin d'un combat
    /// Retire les joueurs qui ne sont plus actifs et qui ne font pas partie du groupe
    /// </summary>
    /// <param name="playersCollection">Collection observable des joueurs à nettoyer</param>
    /// <param name="combatEndTime">Moment où le combat s'est terminé</param>
    /// <param name="skipIfResetting">Si true, ne pas nettoyer si un reset est en cours</param>
    /// <param name="freezeAfterCombat">Si true, ne pas nettoyer car le Kikimeter est figé après le combat</param>
    public void CleanupPlayersAfterCombat(
        System.Collections.ObjectModel.ObservableCollection<PlayerStats> playersCollection,
        DateTime combatEndTime,
        bool skipIfResetting = true,
        bool freezeAfterCombat = false)
    {
        if (playersCollection == null)
            throw new ArgumentNullException(nameof(playersCollection));

        // CRITIQUE: Ne jamais nettoyer pendant un combat actif
        // Le nettoyage doit se faire uniquement après la fin du combat
        if (_dataProvider.IsCombatActive)
        {
            Logger.Debug(LogCategory, "Nettoyage suspendu car un combat est actif - le nettoyage sera effectué après la fin du combat");
            return;
        }

        // CRITIQUE: Ne pas nettoyer si le Kikimeter est figé après le combat
        // Le Kikimeter reste figé pour permettre l'analyse des stats du combat précédent
        // Exécuter uniquement si FreezeAfterCombat = false
        if (freezeAfterCombat)
        {
            Logger.Debug(LogCategory, "Nettoyage suspendu car le Kikimeter est figé après le combat");
            return;
        }

        // Ne pas nettoyer si un reset est en cours
        if (skipIfResetting && _isResetting)
        {
            Logger.Debug(LogCategory, "Nettoyage ignoré car un reset est en cours");
            return;
        }

        // Vérifier le changement de serveur
        if (_dataProvider is JsonPlayerDataProvider jsonProvider)
        {
            var currentServer = jsonProvider.GetCurrentServerName();
            if (!string.IsNullOrEmpty(currentServer) && 
                !string.IsNullOrEmpty(_lastServerName) &&
                !string.Equals(currentServer, _lastServerName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info(LogCategory, $"Changement de serveur détecté ({_lastServerName} -> {currentServer}), nettoyage ignoré (sera géré par le reset serveur)");
                _lastServerName = currentServer;
                return;
            }
            _lastServerName = currentServer;
        }

        _lastCombatEndTime = combatEndTime;
        Logger.Info(LogCategory, $"Nettoyage des joueurs après combat terminé à {combatEndTime:HH:mm:ss}");

        try
        {
            // Récupérer les données actuelles depuis le polling JSON (source de vérité)
            var currentPlayerData = _dataProvider.GetCurrentPlayers();
            var playersInCombat = _dataProvider.GetPlayersInCombat();

            // Mettre à jour l'état des joueurs existants
            UpdatePlayerStates(playersCollection, currentPlayerData, playersInCombat);

            // Identifier les joueurs à retirer
            var playersToRemove = IdentifyPlayersToRemove(playersCollection, currentPlayerData, playersInCombat);

            // Retirer les joueurs qui ne doivent plus être affichés
            RemoveInactivePlayers(playersCollection, playersToRemove);

            // S'assurer que le groupe ne dépasse pas 6 joueurs
            EnforceGroupSizeLimit(playersCollection);

            // Maintenir l'ordre de tour pour les joueurs actifs
            MaintainTurnOrder(playersCollection);
            
            // Notifier les composants du changement
            OnPlayersChanged();

            Logger.Info(LogCategory, $"Nettoyage terminé. {playersCollection.Count} joueurs restants.");
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors du nettoyage des joueurs: {ex.Message}");
        }
    }

    /// <summary>
    /// Met à jour l'état des joueurs existants en fonction des données JSON
    /// </summary>
    private void UpdatePlayerStates(
        System.Collections.ObjectModel.ObservableCollection<PlayerStats> playersCollection,
        Dictionary<string, PlayerData> currentPlayerData,
        HashSet<string> playersInCombat)
    {
        var now = DateTime.Now;

        foreach (var player in playersCollection.ToList())
        {
            // Chercher les données correspondantes dans le JSON
            var playerData = currentPlayerData.Values
                .FirstOrDefault(p => string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(p.Id, player.PlayerId.ToString(), StringComparison.OrdinalIgnoreCase));

            if (playerData != null)
            {
                // Mettre à jour les propriétés depuis le JSON
                player.IsInGroup = playerData.IsInGroup;
                player.IsMainCharacter = playerData.IsMainCharacter;
                player.IsActive = playerData.IsActive;
                player.LastSeenInCombat = playerData.LastSeenInCombat;

                // Si le joueur est dans le combat actuel, mettre à jour LastSeenInCombat
                if (playersInCombat.Contains(player.Name) || playersInCombat.Contains(player.PlayerId.ToString()))
                {
                    player.LastSeenInCombat = now;
                    player.IsActive = true;
                }
            }
            else
            {
                // Joueur non trouvé dans les données JSON
                // Si c'est un joueur du groupe, on le garde mais on le marque comme inactif
                if (player.IsInGroup)
                {
                    player.IsActive = false;
                    Logger.Debug(LogCategory, $"Joueur du groupe '{player.Name}' non trouvé dans JSON, marqué comme inactif");
                }
            }
        }
    }

    /// <summary>
    /// Identifie les joueurs qui doivent être retirés de la collection
    /// </summary>
    private List<PlayerStats> IdentifyPlayersToRemove(
        System.Collections.ObjectModel.ObservableCollection<PlayerStats> playersCollection,
        Dictionary<string, PlayerData> currentPlayerData,
        HashSet<string> playersInCombat)
    {
        var playersToRemove = new List<PlayerStats>();
        var now = DateTime.Now;
        var timeSinceCombatEnd = (now - _lastCombatEndTime).TotalSeconds;

        foreach (var player in playersCollection.ToList())
        {
            bool shouldRemove = false;

            // Règle 1: Ne jamais retirer le personnage principal
            if (player.IsMainCharacter)
            {
                Logger.Debug(LogCategory, $"Personnage principal '{player.Name}' conservé");
                continue;
            }

            // Règle 2: Ne jamais retirer les joueurs du groupe (max 6)
            if (player.IsInGroup)
            {
                Logger.Debug(LogCategory, $"Joueur du groupe '{player.Name}' conservé");
                continue;
            }

            // Règle 3: Retirer les adversaires (joueurs non dans le groupe et non actifs)
            if (!player.IsInGroup && !player.IsActive)
            {
                // Vérifier si le joueur a été vu récemment dans un combat
                var timeSinceLastSeen = (now - player.LastSeenInCombat).TotalSeconds;
                
                if (timeSinceLastSeen > CombatInactivityTimeoutSeconds)
                {
                    shouldRemove = true;
                    Logger.Debug(LogCategory, $"Adversaire '{player.Name}' à retirer (inactif depuis {timeSinceLastSeen:F0}s)");
                }
            }

            // Règle 4: Retirer les joueurs qui ne sont plus dans les données JSON
            // et qui ne sont pas dans le groupe
            if (!player.IsInGroup && !shouldRemove)
            {
                var existsInJson = currentPlayerData.Values.Any(p =>
                    string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Id, player.PlayerId.ToString(), StringComparison.OrdinalIgnoreCase));

                if (!existsInJson && !playersInCombat.Contains(player.Name) && !playersInCombat.Contains(player.PlayerId.ToString()))
                {
                    var timeSinceLastSeen = (now - player.LastSeenInCombat).TotalSeconds;
                    if (timeSinceLastSeen > CombatInactivityTimeoutSeconds)
                    {
                        shouldRemove = true;
                        Logger.Debug(LogCategory, $"Joueur '{player.Name}' non trouvé dans JSON, à retirer");
                    }
                }
            }

            if (shouldRemove)
            {
                playersToRemove.Add(player);
            }
        }

        return playersToRemove;
    }

    /// <summary>
    /// Retire les joueurs inactifs de la collection
    /// </summary>
    private void RemoveInactivePlayers(
        System.Collections.ObjectModel.ObservableCollection<PlayerStats> playersCollection,
        List<PlayerStats> playersToRemove)
    {
        foreach (var player in playersToRemove)
        {
            try
            {
                playersCollection.Remove(player);
                Logger.Info(LogCategory, $"Joueur retiré du Kikimeter : {player.Name}");
                OnPlayersChanged(); // Notifier les composants
            }
            catch (Exception ex)
            {
                Logger.Error(LogCategory, $"Erreur lors de la suppression de '{player.Name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// S'assure que le groupe ne dépasse pas 6 joueurs
    /// Si plus de 6 joueurs sont dans le groupe, on garde les 6 plus récents
    /// </summary>
    private void EnforceGroupSizeLimit(
        System.Collections.ObjectModel.ObservableCollection<PlayerStats> playersCollection)
    {
        var groupPlayers = playersCollection
            .Where(p => p.IsInGroup)
            .OrderByDescending(p => p.LastSeenInCombat)
            .ToList();

        if (groupPlayers.Count <= MaxGroupSize)
            return;

        // Retirer les joueurs du groupe en excès (garder les 6 plus récents)
        var playersToRemoveFromGroup = groupPlayers.Skip(MaxGroupSize).ToList();

        foreach (var player in playersToRemoveFromGroup)
        {
            // Ne pas retirer le personnage principal même s'il dépasse la limite
            if (player.IsMainCharacter)
                continue;

            player.IsInGroup = false;
            Logger.Info(LogCategory, $"Joueur '{player.Name}' retiré du groupe (limite de {MaxGroupSize} atteinte)");

            // Si le joueur n'est plus actif, le retirer complètement
            if (!player.IsActive)
            {
                try
                {
                    playersCollection.Remove(player);
                    Logger.Info(LogCategory, $"Joueur '{player.Name}' retiré (plus dans le groupe et inactif)");
                }
                catch (Exception ex)
                {
                    Logger.Error(LogCategory, $"Erreur lors de la suppression de '{player.Name}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Maintient l'ordre de tour pour les joueurs actifs
    /// Les joueurs sont triés par leur ordre de tour (NumberOfTurns) puis par leur ordre manuel
    /// </summary>
    private void MaintainTurnOrder(
        System.Collections.ObjectModel.ObservableCollection<PlayerStats> playersCollection)
    {
        // Trier les joueurs actifs par leur ordre de tour
        var activePlayers = playersCollection
            .Where(p => p.IsActive)
            .OrderBy(p => p.NumberOfTurns)
            .ThenBy(p => p.ManualOrder)
            .ToList();

        // Réorganiser la collection pour respecter l'ordre de tour
        for (int i = 0; i < activePlayers.Count; i++)
        {
            var player = activePlayers[i];
            var currentIndex = playersCollection.IndexOf(player);

            if (currentIndex != i && currentIndex >= 0)
            {
                try
                {
                    playersCollection.Move(currentIndex, i);
                }
                catch (Exception ex)
                {
                    Logger.Error(LogCategory, $"Erreur lors du déplacement de '{player.Name}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Met à jour un joueur existant avec les données JSON
    /// </summary>
    public void UpdatePlayerFromJson(PlayerStats player, PlayerData playerData)
    {
        if (player == null || playerData == null)
            return;

        player.IsInGroup = playerData.IsInGroup;
        player.IsMainCharacter = playerData.IsMainCharacter;
        player.IsActive = playerData.IsActive;
        player.LastSeenInCombat = playerData.LastSeenInCombat;
    }

    /// <summary>
    /// Synchronise périodiquement les joueurs avec les données JSON
    /// À appeler régulièrement depuis un timer pour maintenir la cohérence
    /// </summary>
    /// <param name="playersCollection">Collection observable des joueurs à synchroniser</param>
    /// <param name="skipIfResetting">Si true, ne pas synchroniser si un reset est en cours</param>
    public void SyncPlayersWithJson(
        System.Collections.ObjectModel.ObservableCollection<PlayerStats> playersCollection,
        bool skipIfResetting = true)
    {
        if (playersCollection == null)
            return;

        // Ne pas synchroniser si un reset est en cours
        if (skipIfResetting && _isResetting)
        {
            return;
        }

        // Vérifier le changement de serveur
        if (_dataProvider is JsonPlayerDataProvider jsonProvider)
        {
            var currentServer = jsonProvider.GetCurrentServerName();
            if (!string.IsNullOrEmpty(currentServer) && 
                !string.IsNullOrEmpty(_lastServerName) &&
                !string.Equals(currentServer, _lastServerName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info(LogCategory, $"Changement de serveur détecté ({_lastServerName} -> {currentServer}), synchronisation ignorée");
                _lastServerName = currentServer;
                return;
            }
            _lastServerName = currentServer;
        }

        try
        {
            var currentPlayerData = _dataProvider.GetCurrentPlayers();
            var playersInCombat = _dataProvider.GetPlayersInCombat();

            // Mettre à jour les joueurs existants
            UpdatePlayerStates(playersCollection, currentPlayerData, playersInCombat);

            // Ajouter les nouveaux joueurs détectés dans le JSON
            foreach (var playerData in currentPlayerData.Values)
            {
                var existing = playersCollection.FirstOrDefault(p =>
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
                        LastSeenInCombat = playerData.LastSeenInCombat
                    };

                    playersCollection.Add(newPlayer);
                    Logger.Info(LogCategory, $"Joueur ajouté au Kikimeter : {playerData.Name}");
                    OnPlayersChanged(); // Notifier les composants
                }
            }
            
            // Sélection automatique du personnage principal si aucun n'est défini
            AutoSelectMainCharacter(playersCollection);

            // Nettoyer les joueurs inactifs si aucun combat n'est actif
            if (!_dataProvider.IsCombatActive)
            {
                var playersToRemove = IdentifyPlayersToRemove(playersCollection, currentPlayerData, playersInCombat);
                if (playersToRemove.Count > 0)
                {
                    RemoveInactivePlayers(playersCollection, playersToRemove);
                    OnPlayersChanged(); // Notifier les composants
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la synchronisation avec JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Marque le service comme étant en cours de reset
    /// Empêche le nettoyage et la synchronisation pendant le reset
    /// </summary>
    public void BeginReset()
    {
        _isResetting = true;
        Logger.Info(LogCategory, "Reset serveur détecté, nettoyage et synchronisation suspendus");
    }

    /// <summary>
    /// Marque la fin du reset
    /// Réactive le nettoyage et la synchronisation
    /// </summary>
    public void EndReset()
    {
        _isResetting = false;
        _lastServerName = null;
        if (_dataProvider is JsonPlayerDataProvider jsonProvider)
        {
            _lastServerName = jsonProvider.GetCurrentServerName();
        }
        Logger.Info(LogCategory, "Reset serveur terminé, nettoyage et synchronisation réactivés");
    }
}
