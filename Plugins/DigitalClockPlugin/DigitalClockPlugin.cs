using System;
using System.Windows;
using GameOverlay.Models;

namespace DigitalClockPlugin
{
    public class DigitalClockPlugin : IPlugin
    {
        private IPluginContext? _context;
        private DigitalClockWindow? _clockWindow;
        private bool _isActive = false;

        public string Name => "Horloge Digitale";
        public string Version => "1.0.0";
        public string Description => "Affiche une horloge digitale déplaçable et redimensionnable. Déplacer avec la souris. Ctrl + Molette pour changer la taille.";
        public string Author => "Amaliassistant";

        public bool IsActive => _isActive;

        public void Initialize(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("Plugin Horloge Digitale initialisé");
        }

        public void Activate()
        {
            if (_context == null)
            {
                return;
            }

            try
            {
                if (_clockWindow == null)
                {
                    var configPath = System.IO.Path.Combine(_context.PluginDataDirectory, "config.json");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _clockWindow = new DigitalClockWindow(_context, configPath);
                        _clockWindow.Closed += (s, e) => 
                        { 
                            // Si la fenêtre se ferme, ne pas la mettre à null si le plugin est toujours actif
                            if (!_isActive)
                            {
                                _clockWindow = null;
                            }
                        };
                        // Afficher la fenêtre et s'assurer qu'elle reste visible
                        _clockWindow.Show();
                        _clockWindow.Visibility = Visibility.Visible;
                        _clockWindow.Activate();
                    });
                }

                _isActive = true;
                _context.Logger.Info("Horloge digitale activée");
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Erreur lors de l'activation: {ex.Message}");
            }
        }

        public void Deactivate()
        {
            if (_context == null)
            {
                return;
            }

            try
            {
                // Ne cacher la fenêtre que si on désactive vraiment le plugin
                if (_clockWindow != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _clockWindow.Hide();
                    });
                }

                _isActive = false;
                _context.Logger.Info("Horloge digitale désactivée");
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Erreur lors de la désactivation: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            try
            {
                if (_clockWindow != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _clockWindow.Close();
                        _clockWindow = null;
                    });
                }

                _context?.Logger.Info("Plugin Horloge Digitale nettoyé");
                _context = null;
                _isActive = false;
            }
            catch (Exception ex)
            {
                _context?.Logger.Error($"Erreur lors du nettoyage: {ex.Message}");
            }
        }
    }
}

