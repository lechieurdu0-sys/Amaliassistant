using System;
using System.IO;
using GameOverlay.Kikimeter.Core;
using GameOverlay.Models;
using GameOverlay.Kikimeter;

namespace GameOverlay.Kikimeter.Services;

public class LogFileWatcher
{
    private const string LogCategory = "LogFileWatcher";

    private FileSystemWatcher? _fileWatcher;
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
            Logger.Warning(LogCategory, "Le chemin de log est vide ou null");
            LogFileNotFound?.Invoke(this, EventArgs.Empty);
            return;
        }
            
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
            
            // Créer le FileSystemWatcher même si le fichier n'existe pas encore
            // Cela permet de détecter quand le fichier sera créé
            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            
            _fileWatcher.Changed += (_, e) => HandleWatcherEvent(e.ChangeType, e.FullPath);
            _fileWatcher.Created += (_, e) => 
            {
                Logger.Info(LogCategory, $"Fichier de log créé détecté: {e.FullPath}");
                // Lire le fichier nouvellement créé
                System.Threading.Tasks.Task.Run(() => ReadExistingLog());
                HandleWatcherEvent(e.ChangeType, e.FullPath, resetPosition: true);
            };
            _fileWatcher.Deleted += (_, e) => HandleWatcherEvent(e.ChangeType, e.FullPath, resetPosition: true, skipRead: true);
            _fileWatcher.Renamed += (_, e) => HandleWatcherEvent(e.ChangeType, e.FullPath, resetPosition: true);
            _fileWatcher.Error += (_, e) => HandleWatcherError(e);
            
            _fileWatcher.EnableRaisingEvents = true;
            WatcherDiagnostics.LogFileEvent("WatcherReady", $"FileSystemWatcher initialisé pour {_logPath} (fichier existe: {File.Exists(_logPath)})");
            Logger.Info(LogCategory, $"Surveillance du fichier '{fileName}' dans '{directory}' démarrée (fichier existe: {File.Exists(_logPath)})");
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
    
    private void ReadNewLines()
    {
        if (_logPath == null || !File.Exists(_logPath))
            return;
        
        // Éviter les lectures simultanées (FileSystemWatcher peut déclencher Changed plusieurs fois)
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
            using var reader = new StreamReader(new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var fileLength = reader.BaseStream.Length;
            WatcherDiagnostics.LogReadStatus("BeforeRead", _lastPosition, fileLength);

            if (_lastPosition > fileLength)
            {
                Logger.Info(LogCategory, $"Troncature détectée sur le log, réinitialisation de la position (ancienne={_lastPosition}, nouvelle taille={reader.BaseStream.Length}).");
                WatcherDiagnostics.LogFileEvent("TruncationDetected", $"Ancienne position={_lastPosition}, nouvelle taille={reader.BaseStream.Length}");
                _lastPosition = 0;
                _logParser.Reset();
                WatcherDiagnostics.LogFileEvent("ParserReset", "Reset suite à troncature détectée");
            }

            if (_lastPosition < 0)
            {
                _lastPosition = 0;
            }

            reader.BaseStream.Seek(_lastPosition, SeekOrigin.Begin);
            
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                _logParser.ProcessLogLine(line);
                LogLineProcessed?.Invoke(this, line);
                Logger.Debug(LogCategory, $"Ligne transmise au parseur: {line}");
            }
            
            _lastPosition = reader.BaseStream.Position;
            WatcherDiagnostics.LogReadStatus("AfterRead", _lastPosition, fileLength);
            Logger.Debug(LogCategory, $"Lecture terminée, nouvelle position: {_lastPosition}");

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
        if (_fileWatcher != null)
                                {
            WatcherDiagnostics.LogStopWatching(_logPath);
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
            Logger.Info(LogCategory, "Surveillance des logs arrêtée");
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

        try
        {
            System.Threading.Tasks.Task.Run(() => ReadNewLines());
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

