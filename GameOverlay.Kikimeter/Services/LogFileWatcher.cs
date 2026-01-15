using System;
using System.IO;
using System.Threading;
using GameOverlay.Kikimeter.Core;
using GameOverlay.Models;
using GameOverlay.Kikimeter;

namespace GameOverlay.Kikimeter.Services;

public class LogFileWatcher : IDisposable
{
    private const string LogCategory = "LogFileWatcher";
    private const int PollingIntervalMs = 300; // 300ms entre chaque vérification (dans la fourchette 250-500ms)

    private FileSystemWatcher? _fileWatcher;
    private System.Threading.Timer? _pollingTimer;
    private string? _logPath;
    private Core.LogParser _logParser;
    
    public Core.LogParser Parser => _logParser;
    
    public event EventHandler<string>? LogLineProcessed;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? LogFileNotFound;
    
    public LogFileWatcher()
    {
        _logParser = new Core.LogParser();
        Logger.Info(LogCategory, "Instance de surveillance des logs créée");
    }
    
    public void StartWatching(string logPath)
    {
        _logPath = logPath;
        WatcherDiagnostics.LogStartWatching(_logPath);
        
        if (string.IsNullOrEmpty(_logPath))
        {
            WatcherDiagnostics.LogFileEvent("FileMissing", "Chemin de log vide ou null");
            Logger.Warning(LogCategory, "Le chemin de log est vide ou null - la surveillance ne peut pas démarrer");
            LogFileNotFound?.Invoke(this, EventArgs.Empty);
            return;
        }
        
        Logger.Info(LogCategory, $"Démarrage de la surveillance pour: '{_logPath}'");
            
        try
        {
            // Surveiller les changements
            var directory = Path.GetDirectoryName(_logPath);
            var fileName = Path.GetFileName(_logPath);
            
            if (directory == null || string.IsNullOrEmpty(fileName))
            {
                Logger.Error(LogCategory, $"Impossible de déterminer le répertoire ou le nom de fichier pour: {_logPath}");
                return;
            }
            
            // Vérifier si le dossier existe
            if (!Directory.Exists(directory))
            {
                Logger.Warning(LogCategory, $"Le répertoire n'existe pas: {directory}. La surveillance sera activée mais ne fonctionnera que lorsque le dossier sera créé.");
            }
            
            // Lire le fichier existant de manière asynchrone si le fichier existe déjà
            if (File.Exists(_logPath))
            {
                System.Threading.Tasks.Task.Run(() => ReadExistingLog());
                Logger.Info(LogCategory, $"Fichier existant détecté, lecture initiale démarrée");
            }
            else
            {
                Logger.Info(LogCategory, $"Fichier de log n'existe pas encore: '{_logPath}'. Surveillance activée pour détecter sa création.");
            }
            
            // Créer le FileSystemWatcher pour surveiller le DOSSIER (pas le fichier unique)
            // Il sert uniquement de "réveil" optionnel, le polling est la source de vérité
            _fileWatcher = new FileSystemWatcher(directory)
            {
                Filter = "*.log",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            
            _fileWatcher.Changed += (_, e) => 
            {
                // Vérifier que c'est bien notre fichier
                if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    // Réveil optionnel : déclencher une lecture immédiate (mais le polling continue)
                    Logger.Debug(LogCategory, $"Événement FileSystemWatcher détecté (réveil optionnel): {e.FullPath}");
                    System.Threading.Tasks.Task.Run(() => PollAndRead());
                }
            };
            _fileWatcher.Created += (_, e) => 
            {
                if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info(LogCategory, $"Fichier de log créé détecté (réveil optionnel): {e.FullPath}");
                    System.Threading.Tasks.Task.Run(() => ReadExistingLog());
                    ResetParserState("fichier créé");
                }
            };
            _fileWatcher.Deleted += (_, e) => 
            {
                if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info(LogCategory, $"Fichier de log supprimé détecté: {e.FullPath}");
                    ResetParserState("fichier supprimé");
                }
            };
            _fileWatcher.Renamed += (_, e) => 
            {
                if (Path.GetFileName(e.FullPath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info(LogCategory, $"Fichier de log renommé détecté: {e.FullPath}");
                    ResetParserState("fichier renommé");
                }
            };
            _fileWatcher.Error += (_, e) => HandleWatcherError(e);
            
            _fileWatcher.EnableRaisingEvents = true;
            WatcherDiagnostics.LogFileEvent("WatcherReady", $"FileSystemWatcher initialisé pour le dossier {directory} (fichier cible: {fileName})");
            Logger.Info(LogCategory, $"FileSystemWatcher initialisé pour surveiller le dossier '{directory}' (fichier cible: {fileName})");
            
            // POLLING ROBUSTE : source de vérité principale
            // Ne dépend plus de FileSystemWatcher - le polling lit toujours les données
            _pollingTimer = new System.Threading.Timer(PollingTimerCallback, null, PollingIntervalMs, PollingIntervalMs);
            Logger.Info(LogCategory, $"Polling robuste démarré (intervalle: {PollingIntervalMs}ms) - source de vérité principale");
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors du démarrage de la surveillance: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }
    
    private void ReadExistingLog()
        {
        if (_logPath == null || !File.Exists(_logPath))
            return;
            
        try
        {
            // Pour éviter de lire tout le fichier et bloquer, on se positionne à la fin
            // On ne lit que les nouvelles lignes qui seront ajoutées après
            var fileInfo = new FileInfo(_logPath);
            if (fileInfo.Length > 0)
            {
                using var reader = new StreamReader(new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                // Se positionner à la fin du fichier pour ignorer l'historique
                reader.BaseStream.Seek(0, SeekOrigin.End);
                _lastPosition = reader.BaseStream.Position;
                _lastKnownFileLength = fileInfo.Length;
                Logger.Debug(LogCategory, $"Lecture initiale ignorée (positionne à la fin). Taille fichier: {fileInfo.Length} octets");
            }
            else
            {
                Logger.Debug(LogCategory, "Fichier vide détecté lors de la lecture initiale");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la lecture initiale du log: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }
    
    private long _lastPosition = 0;
    private bool _isReading = false;
    private readonly object _readLock = new object();
    
    /// <summary>
    /// Callback du timer de polling - méthode principale de lecture
    /// </summary>
    private void PollingTimerCallback(object? state)
    {
        PollAndRead();
    }
    
    /// <summary>
    /// Méthode de polling robuste : vérifie FileInfo.Length et lit si nécessaire
    /// </summary>
    private void PollAndRead()
    {
        if (string.IsNullOrEmpty(_logPath))
            return;
        
        // Éviter les lectures simultanées
        lock (_readLock)
        {
            if (_isReading)
                return; // Déjà en cours de lecture, ignorer cet appel
        }
        
        // Vérifier que le fichier existe
        if (!File.Exists(_logPath))
        {
            // Le fichier n'existe pas encore, on attend
            return;
        }
        
        try
        {
            // Lire FileInfo.Length pour détecter les changements
            var fileInfo = new FileInfo(_logPath);
            long currentLength = fileInfo.Length;
            
            // Si Length > lastPosition, il y a de nouvelles données à lire
            if (currentLength > _lastPosition)
            {
                // Ouvrir, lire, fermer - ne jamais garder le FileStream ouvert
                ReadNewLines();
            }
            // Si Length < lastPosition, le fichier a été recréé ou tronqué
            else if (currentLength < _lastPosition)
            {
                Logger.Info(LogCategory, $"Fichier recréé/tronqué détecté (polling) - ancienne taille={_lastPosition}, nouvelle taille={currentLength}");
                ResetParserState("fichier recréé/tronqué (polling)");
                // Relire depuis le début
                ReadNewLines();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors du polling: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Lit les nouvelles lignes du fichier (ouvre, lit, ferme)
    /// </summary>
    private void ReadNewLines()
    {
        if (_logPath == null || !File.Exists(_logPath))
            return;
        
        // Éviter les lectures simultanées
        lock (_readLock)
        {
            if (_isReading)
                return; // Déjà en cours de lecture, ignorer cet appel
            
            _isReading = true;
            Logger.Debug(LogCategory, "Début de lecture incrémentale du log");
        }
        
        try
        {
            var previousLength = _lastKnownFileLength;
            
            // Ouvrir le flux (ne jamais le garder ouvert en permanence)
            using var reader = new StreamReader(new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var fileLength = reader.BaseStream.Length;
            WatcherDiagnostics.LogReadStatus("BeforeRead", _lastPosition, fileLength);

            // Gérer la recréation / rotation du fichier
            if (_lastPosition > fileLength)
            {
                Logger.Info(LogCategory, $"Troncature détectée sur le log, réinitialisation de la position (ancienne={_lastPosition}, nouvelle taille={fileLength}).");
                WatcherDiagnostics.LogFileEvent("TruncationDetected", $"Ancienne position={_lastPosition}, nouvelle taille={fileLength}");
                _lastPosition = 0;
                _logParser.Reset();
                WatcherDiagnostics.LogFileEvent("ParserReset", "Reset suite à troncature détectée");
            }

            if (_lastPosition < 0)
            {
                _lastPosition = 0;
            }

            // Se positionner à la dernière position connue
            reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
            
            // Lire ligne par ligne jusqu'à EOF
            string? line;
            int linesRead = 0;
            while ((line = reader.ReadLine()) != null)
            {
                _logParser.ProcessLogLine(line);
                LogLineProcessed?.Invoke(this, line);
                linesRead++;
            }
            
            // Mettre à jour lastPosition
            _lastPosition = reader.BaseStream.Position;
            WatcherDiagnostics.LogReadStatus("AfterRead", _lastPosition, fileLength);
            
            if (linesRead > 0)
            {
                Logger.Debug(LogCategory, $"Lecture terminée: {linesRead} lignes lues, nouvelle position: {_lastPosition}");
            }

            if (_lastPosition == fileLength && previousLength > 0 && fileLength < previousLength)
            {
                // Le fichier s'est raccourci mais nous sommes déjà à la fin, on force une remonte à zéro.
                WatcherDiagnostics.LogFileEvent("LengthShrinkDetected", $"Ancienne taille={previousLength}, nouvelle taille={fileLength}, position={_lastPosition}. Reset forcé.");
                _lastPosition = 0;
                _logParser.Reset();
                WatcherDiagnostics.LogFileEvent("ParserReset", "Reset suite à détection de rétrécissement de fichier");
            }

            _lastKnownFileLength = fileLength;
        }
        catch (IOException ioEx)
        {
            // Fichier verrouillé par le jeu - c'est normal, on réessayera au prochain tick
            Logger.Debug(LogCategory, $"Fichier verrouillé (normal), réessai au prochain tick: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            // Problème de permissions - log mais ne pas bloquer
            Logger.Warning(LogCategory, $"Problème de permissions: {uaEx.Message}");
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la lecture incrémentale: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally
        {
            lock (_readLock)
            {
                _isReading = false;
                Logger.Debug(LogCategory, "Lecture incrémentale terminée");
                if (_resetAfterRead)
                {
                    _resetAfterRead = false;
                    ResetParserState("reset différé (post-lecture)");
                }
            }
        }
    }
    
    public void ManualRead()
                {
        // Lire de manière asynchrone pour éviter de bloquer l'UI
        Logger.Info(LogCategory, "Lecture manuelle demandée");
        System.Threading.Tasks.Task.Run(() => ReadNewLines());
    }
    
    public void StopWatching()
    {
        // Arrêter le polling (source de vérité)
        if (_pollingTimer != null)
        {
            _pollingTimer.Dispose();
            _pollingTimer = null;
            Logger.Info(LogCategory, "Polling arrêté");
        }
        
        // Arrêter le FileSystemWatcher (optionnel)
        if (_fileWatcher != null)
        {
            WatcherDiagnostics.LogStopWatching(_logPath);
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
            Logger.Info(LogCategory, "FileSystemWatcher arrêté");
        }
    }
    
    public void Dispose()
    {
        StopWatching();
        Logger.Debug(LogCategory, "Dispose appelé sur LogFileWatcher");
    }

    private long _lastKnownFileLength = 0;

    private void HandleWatcherEvent(WatcherChangeTypes changeType, string fullPath, bool resetPosition = false, bool skipRead = false)
    {
        WatcherDiagnostics.LogFileEvent(changeType.ToString(), $"Événement sur {fullPath} (reset={resetPosition}, skipRead={skipRead})");
        Logger.Debug(LogCategory, $"Événement FileSystemWatcher détecté: {changeType} sur {fullPath} (reset={resetPosition}, skipRead={skipRead})");

        if (resetPosition)
        {
            PrepareResetAfterEvent(changeType);
        }

        if (skipRead)
        {
            return;
        }

        // Le FileSystemWatcher sert uniquement de réveil optionnel
        // Le polling continuera de toute façon à lire les données
        try
        {
            // Déclencher une lecture immédiate (mais le polling continue)
            System.Threading.Tasks.Task.Run(() => PollAndRead());
        }
        catch (Exception ex)
        {
            Logger.Error(LogCategory, $"Erreur lors de la lecture suite à un événement {changeType}: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }

    private void HandleWatcherError(ErrorEventArgs e)
    {
        var message = e.GetException()?.Message ?? "Erreur inconnue";
        WatcherDiagnostics.LogFileEvent("WatcherError", message);
        Logger.Error(LogCategory, $"Erreur FileSystemWatcher: {message}");

        if (!string.IsNullOrEmpty(_logPath))
        {
            // Tenter une relance discrète
            StopWatching();
            StartWatching(_logPath);
        }
    }

    private bool _resetAfterRead = false;

    private void PrepareResetAfterEvent(WatcherChangeTypes changeType)
    {
        lock (_readLock)
        {
            if (_isReading)
            {
                _resetAfterRead = true;
                WatcherDiagnostics.LogFileEvent("DeferredReset", $"Reset différé en raison d'une lecture en cours ({changeType})");
            }
            else
            {
                ResetParserState($"événement {changeType}");
            }
        }
    }

    private void ResetParserState(string reason)
    {
        _lastPosition = 0;
        _lastKnownFileLength = 0;
        _logParser.Reset();
        WatcherDiagnostics.LogFileEvent("ParserReset", $"Reset immédiat suite à {reason}");
    }
}

