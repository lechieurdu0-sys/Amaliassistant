using System;
using System.Linq;
using GameOverlay.Models;

namespace GameOverlay.Kikimeter;

public partial class KikimeterWindow
{
    public void ResetDisplayFromLoot(string reason)
    {
        void PerformReset()
        {
            // CRITIQUE: Ne jamais faire de reset complet pendant un combat actif
            // Cela viderait la collection des joueurs en plein combat, causant un flash/blanc
            if (_playerDataProvider != null && _playerDataProvider.IsCombatActive)
            {
                Logger.Debug("KikimeterWindow", $"Reset suspendu car un combat est actif ({reason}) - le reset sera effectué après la fin du combat");
                return;
            }

            // CRITIQUE: Ne pas faire de reset si le Kikimeter est figé après le combat
            // Le Kikimeter reste figé pour permettre l'analyse des stats du combat précédent
            // MAIS permettre le reset si c'est un nouveau combat (initialisation)
            if (_freezeAfterCombat && !_isNewCombat)
            {
                Logger.Debug("KikimeterWindow", $"Reset suspendu car le Kikimeter est figé après le combat ({reason})");
                return;
            }

            Logger.Info("KikimeterWindow", $"Reset manuel déclenché ({reason})");

            // Marquer le reset comme en cours AVANT toute opération
            _isResetInProgress = true;

            // Notifier le service de gestion des joueurs qu'un reset est en cours
            // Cela empêche le nettoyage automatique pendant le reset
            if (_playerManagementService != null)
            {
                _playerManagementService.BeginReset();
            }

            try
            {
                CloseAllIndividualWindows();
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Erreur lors de la fermeture des fenêtres individuelles: {ex.Message}");
            }

            foreach (var player in _playersCollection.ToList())
            {
                try
                {
                    player.PropertyChanged -= Player_PropertyChanged;
                }
                catch (Exception ex)
                {
                    Logger.Debug("KikimeterWindow", $"Impossible de détacher Player_PropertyChanged pour {player.Name}: {ex.Message}");
                }
            }

            _playersCollection.Clear();
            _playerKikis.Clear();

            if (_indicatorWindow != null)
            {
                try
                {
                    _indicatorWindow.Close();
                }
                catch
                {
                    // ignore
                }

                _indicatorWindow = null;
            }

            try
            {
                if (_logWatcher?.Parser != null)
                {
                    _logWatcher.Parser.PlayerStats.Clear();
                    _logWatcher.Parser.Reset();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Erreur lors du reset du parseur: {ex.Message}");
            }

            _displayedMaxDamage = 10000;
            _displayedMaxDamageTaken = 10000;
            _displayedMaxHealing = 10000;
            _displayedMaxShield = 10000;
            _maxDamage = 10000;
            _maxDamageTaken = 10000;
            _maxHealing = 10000;
            _maxShield = 10000;

            try
            {
                ResetAllXpTracking();
            }
            catch (Exception ex)
            {
                Logger.Error("KikimeterWindow", $"Erreur lors du reset XP: {ex.Message}");
            }

            UpdatePreviewVisibility();

            if (CombatStatusText != null)
            {
                CombatStatusText.Text = "En attente d'un nouveau combat";
            }

            // Réactiver le service de gestion des joueurs après le reset
            if (_playerManagementService != null)
            {
                _playerManagementService.EndReset();
            }

            // Marquer le reset comme terminé APRÈS toutes les opérations
            _isResetInProgress = false;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke((Action)PerformReset);
        }
        else
        {
            PerformReset();
        }
    }
}
 
