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
            Logger.Info("KikimeterWindow", $"Reset manuel déclenché ({reason})");

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
 
