using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GameOverlay.Models
{
    /// <summary>
    /// Système de logging avancé avec rotation de fichiers, archivage ZIP et gestion de l'espace disque
    /// </summary>
    public static class AdvancedLogger
    {
        private const int MaxFileSizeBytes = 1024 * 1024; // 1024 Ko (1 Mo)
        private const int MaxFilesPerCategory = 3;
        private const long MaxTotalSizeBytes = 1024L * 1024L * 1024L; // 1 Go
        private const string LogExtension = ".log";
        private const string ZipExtension = ".zip";

        private static readonly string BaseLogDirectory;
        private static readonly Dictionary<string, CategoryLogger> _categoryLoggers = new();
        private static readonly object _globalLock = new object();
        private static System.Threading.Timer? _cleanupTimer;
        private static DateTime _lastHeartbeat = DateTime.Now;

        static AdvancedLogger()
        {
            try
            {
                BaseLogDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Amaliassistant",
                    "logs"
                );

                Directory.CreateDirectory(BaseLogDirectory);

                // Démarrer le timer de nettoyage (vérifie toutes les heures)
                _cleanupTimer = new System.Threading.Timer(CleanupOldArchives, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

                // Démarrer le heartbeat pour détecter les freezes
                var heartbeatTimer = new System.Threading.Timer(HeartbeatCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

                // Ne pas utiliser Log() ici pour éviter les références circulaires
                System.Diagnostics.Debug.WriteLine("[AdvancedLogger] Système de logging avancé initialisé");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERREUR INIT AdvancedLogger: {ex.Message}");
            }
        }

        private static void HeartbeatCallback(object? state)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastHeartbeat).TotalSeconds;

            if (elapsed > 10)
            {
                // Utiliser directement le logger de catégorie pour éviter les références circulaires
                try
                {
                    var categoryLogger = GetOrCreateCategoryLogger("SYSTEM");
                    categoryLogger.Log(LogLevel.Critical, "Heartbeat", $"⚠️ FREEZE DÉTECTÉ ! Dernier heartbeat il y a {elapsed:F1} secondes", null);
                }
                catch
                {
                    // En cas d'erreur, utiliser Debug.WriteLine comme fallback
                    System.Diagnostics.Debug.WriteLine($"[HEARTBEAT] ⚠️ FREEZE DÉTECTÉ ! Dernier heartbeat il y a {elapsed:F1} secondes");
                }
            }

            _lastHeartbeat = now;
        }

        /// <summary>
        /// Marque un heartbeat pour indiquer que le système est vivant
        /// </summary>
        public static void Heartbeat(string category, string module)
        {
            _lastHeartbeat = DateTime.Now;
        }

        /// <summary>
        /// Nettoie les archives anciennes si le dossier dépasse 1 Go
        /// </summary>
        private static void CleanupOldArchives(object? state)
        {
            try
            {
                lock (_globalLock)
                {
                    long totalSize = 0;
                    var zipFiles = new List<FileInfo>();

                    // Calculer la taille totale et lister les fichiers ZIP
                    foreach (var file in Directory.GetFiles(BaseLogDirectory, $"*{ZipExtension}", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            totalSize += fileInfo.Length;
                            zipFiles.Add(fileInfo);
                        }
                        catch { }
                    }

                    // Si on dépasse 1 Go, supprimer les ZIP les plus anciens
                    if (totalSize > MaxTotalSizeBytes)
                    {
                        // Trier par date de création (plus anciens en premier)
                        zipFiles.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));

                        long sizeToRemove = totalSize - MaxTotalSizeBytes;
                        long removedSize = 0;

                        foreach (var zipFile in zipFiles)
                        {
                            if (removedSize >= sizeToRemove)
                                break;

                            try
                            {
                                removedSize += zipFile.Length;
                                File.Delete(zipFile.FullName);
                                // Ne pas logger ici pour éviter les références circulaires
                                System.Diagnostics.Debug.WriteLine($"[Cleanup] Archive supprimée pour libérer de l'espace: {zipFile.Name} ({zipFile.Length / 1024} Ko)");
                            }
                            catch (Exception ex)
                            {
                                Log("SYSTEM", LogLevel.Warning, "Cleanup", $"Impossible de supprimer l'archive {zipFile.Name}: {ex.Message}");
                            }
                        }

                        // Ne pas logger ici pour éviter les références circulaires
                        System.Diagnostics.Debug.WriteLine($"[Cleanup] Nettoyage terminé: {removedSize / 1024 / 1024} Mo supprimés");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERREUR CLEANUP: {ex.Message}");
            }
        }

        /// <summary>
        /// Écrit un log avec toutes les informations de contexte
        /// </summary>
        public static void Log(string category, LogLevel level, string module, string message, Exception? exception = null)
        {
            try
            {
                var categoryLogger = GetOrCreateCategoryLogger(category);
                categoryLogger.Log(level, module, message, exception);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERREUR LOG: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtient ou crée un logger pour une catégorie
        /// </summary>
        private static CategoryLogger GetOrCreateCategoryLogger(string category)
        {
            lock (_globalLock)
            {
                if (!_categoryLoggers.TryGetValue(category, out var logger))
                {
                    logger = new CategoryLogger(category, BaseLogDirectory);
                    _categoryLoggers[category] = logger;
                }
                return logger;
            }
        }

        /// <summary>
        /// Ferme tous les loggers proprement
        /// </summary>
        public static void Close()
        {
            lock (_globalLock)
            {
                try
                {
                    _cleanupTimer?.Dispose();
                    _cleanupTimer = null;

                    foreach (var logger in _categoryLoggers.Values)
                    {
                        logger.Dispose();
                    }

                    _categoryLoggers.Clear();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERREUR FERMETURE AdvancedLogger: {ex.Message}");
                }
            }
        }

        // Méthodes de convenance pour chaque niveau de log
        public static void Debug(string category, string module, string message) =>
            Log(category, LogLevel.Debug, module, message);

        public static void Info(string category, string module, string message) =>
            Log(category, LogLevel.Info, module, message);

        public static void Warning(string category, string module, string message) =>
            Log(category, LogLevel.Warning, module, message);

        public static void Warning(string category, string module, string message, Exception ex) =>
            Log(category, LogLevel.Warning, module, message, ex);

        public static void Error(string category, string module, string message) =>
            Log(category, LogLevel.Error, module, message);

        public static void Error(string category, string module, string message, Exception ex) =>
            Log(category, LogLevel.Error, module, message, ex);

        public static void Critical(string category, string module, string message) =>
            Log(category, LogLevel.Critical, module, message);

        public static void Critical(string category, string module, string message, Exception ex) =>
            Log(category, LogLevel.Critical, module, message, ex);

        /// <summary>
        /// Log une opération avec mesure de temps
        /// </summary>
        public static IDisposable Measure(string category, string module, string operation)
        {
            var sw = Stopwatch.StartNew();
            Info(category, module, $"START: {operation}");
            return new TimingLogger(category, module, operation, sw);
        }

        private class TimingLogger : IDisposable
        {
            private readonly string _category;
            private readonly string _module;
            private readonly string _operation;
            private readonly Stopwatch _sw;

            public TimingLogger(string category, string module, string operation, Stopwatch sw)
            {
                _category = category;
                _module = module;
                _operation = operation;
                _sw = sw;
            }

            public void Dispose()
            {
                _sw.Stop();
                var elapsed = _sw.ElapsedMilliseconds;
                if (elapsed > 100)
                {
                    Warning(_category, _module, $"END (SLOW): {_operation} - {elapsed}ms");
                }
                else
                {
                    Info(_category, _module, $"END: {_operation} - {elapsed}ms");
                }
            }
        }

        /// <summary>
        /// Logger pour une catégorie spécifique avec rotation de fichiers
        /// </summary>
        private class CategoryLogger : IDisposable
        {
            private readonly string _category;
            private readonly string _categoryDirectory;
            private readonly object _lock = new object();
            private StreamWriter? _currentWriter;
            private string _currentFile = "";
            private int _currentFileIndex = 0;

            public CategoryLogger(string category, string baseDirectory)
            {
                _category = category;
                // Créer un dossier spécifique pour chaque catégorie
                _categoryDirectory = Path.Combine(baseDirectory, category);
                Directory.CreateDirectory(_categoryDirectory);
                InitializeCurrentFile();
            }

            /// <summary>
            /// Initialise le fichier de log actuel
            /// </summary>
            private void InitializeCurrentFile()
            {
                try
                {
                    // Chercher le dernier fichier de log existant
                    var existingFiles = Directory.GetFiles(_categoryDirectory, $"*{LogExtension}")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .ToList();

                    if (existingFiles.Count > 0)
                    {
                        var lastFile = existingFiles[0];
                        var fileSize = lastFile.Length;

                        if (fileSize < MaxFileSizeBytes)
                        {
                            // Utiliser le dernier fichier s'il n'est pas plein
                            _currentFile = lastFile.FullName;
                            _currentFileIndex = ExtractFileIndex(lastFile.Name);
                        }
                        else
                        {
                            // Le dernier fichier est plein, créer un nouveau
                            _currentFileIndex = GetNextFileIndex(existingFiles);
                            _currentFile = GetFileName(_currentFileIndex);
                        }
                    }
                    else
                    {
                        // Aucun fichier existant, commencer à l'index 0
                        _currentFileIndex = 0;
                        _currentFile = GetFileName(0);
                    }

                    // Ouvrir le fichier en mode append
                    _currentWriter = new StreamWriter(_currentFile, append: true, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERREUR INIT FILE: {ex.Message}");
                }
            }

            /// <summary>
            /// Obtient le nom de fichier pour un index donné
            /// </summary>
            private string GetFileName(int index)
            {
                return Path.Combine(_categoryDirectory, $"{_category}_{index:D2}{LogExtension}");
            }

            /// <summary>
            /// Extrait l'index d'un nom de fichier
            /// </summary>
            private int ExtractFileIndex(string fileName)
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var parts = nameWithoutExt.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out var index))
                {
                    return index;
                }
                return 0;
            }

            /// <summary>
            /// Obtient le prochain index de fichier disponible
            /// </summary>
            private int GetNextFileIndex(List<FileInfo> existingFiles)
            {
                if (existingFiles.Count == 0)
                    return 0;

                var maxIndex = existingFiles.Max(f => ExtractFileIndex(f.Name));
                return (maxIndex + 1) % MaxFilesPerCategory;
            }

            /// <summary>
            /// Écrit un log avec toutes les informations de contexte
            /// </summary>
            public void Log(LogLevel level, string module, string message, Exception? exception = null)
            {
                lock (_lock)
                {
                    try
                    {
                        // Vérifier si le fichier actuel est trop grand
                        if (_currentWriter != null)
                        {
                            _currentWriter.Flush();
                            var fileInfo = new FileInfo(_currentFile);
                            if (fileInfo.Exists && fileInfo.Length >= MaxFileSizeBytes)
                            {
                                RotateFiles();
                            }
                        }

                        // Construire le message de log avec toutes les informations
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        var threadId = Thread.CurrentThread.ManagedThreadId;
                        var threadName = Thread.CurrentThread.Name ?? "Unknown";

                        var logEntry = new StringBuilder();
                        logEntry.AppendLine($"[{timestamp}] [{level}] [{_category}] [{module}] [Thread:{threadId}/{threadName}] {message}");

                        // Ajouter les informations d'exception si présentes
                        if (exception != null)
                        {
                            logEntry.AppendLine($"  └─ Exception Type: {exception.GetType().FullName}");
                            logEntry.AppendLine($"  └─ Exception Message: {exception.Message}");
                            
                            if (!string.IsNullOrEmpty(exception.Source))
                            {
                                logEntry.AppendLine($"  └─ Source: {exception.Source}");
                            }

                            // Stack trace complet
                            if (exception.StackTrace != null)
                            {
                                logEntry.AppendLine($"  └─ Stack Trace:");
                                var stackLines = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                                foreach (var line in stackLines)
                                {
                                    logEntry.AppendLine($"      {line}");
                                }
                            }

                            // Inner exception
                            if (exception.InnerException != null)
                            {
                                logEntry.AppendLine($"  └─ Inner Exception:");
                                logEntry.AppendLine($"      Type: {exception.InnerException.GetType().FullName}");
                                logEntry.AppendLine($"      Message: {exception.InnerException.Message}");
                                if (exception.InnerException.StackTrace != null)
                                {
                                    logEntry.AppendLine($"      Stack Trace:");
                                    var innerStackLines = exception.InnerException.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                                    foreach (var line in innerStackLines)
                                    {
                                        logEntry.AppendLine($"          {line}");
                                    }
                                }
                            }
                        }

                        // Ajouter le stack trace de l'appelant pour les erreurs critiques
                        if (level == LogLevel.Critical || level == LogLevel.Error)
                        {
                            var stackTrace = new StackTrace(2, true); // Skip 2 frames (Log method + caller)
                            if (stackTrace.FrameCount > 0)
                            {
                                logEntry.AppendLine($"  └─ Call Stack:");
                                for (int i = 0; i < Math.Min(stackTrace.FrameCount, 10); i++)
                                {
                                    var frame = stackTrace.GetFrame(i);
                                    if (frame != null)
                                    {
                                        var method = frame.GetMethod();
                                        var fileName = frame.GetFileName();
                                        var lineNumber = frame.GetFileLineNumber();
                                        logEntry.AppendLine($"      [{i}] {method?.DeclaringType?.FullName}.{method?.Name}()");
                                        if (!string.IsNullOrEmpty(fileName) && lineNumber > 0)
                                        {
                                            logEntry.AppendLine($"          at {Path.GetFileName(fileName)}:{lineNumber}");
                                        }
                                    }
                                }
                            }
                        }

                        // Écrire dans le fichier
                        if (_currentWriter != null)
                        {
                            _currentWriter.WriteLine(logEntry.ToString());
                            _currentWriter.Flush();
                        }

                        // Également écrire dans Debug pour Visual Studio
                        System.Diagnostics.Debug.WriteLine(logEntry.ToString());
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ERREUR ÉCRITURE LOG: {ex.Message}");
                    }
                }
            }

            /// <summary>
            /// Effectue la rotation des fichiers de log
            /// </summary>
            private void RotateFiles()
            {
                try
                {
                    // Fermer le fichier actuel
                    _currentWriter?.Flush();
                    _currentWriter?.Close();
                    _currentWriter?.Dispose();
                    _currentWriter = null;

                    // Déterminer les fichiers à archiver
                    var filesToArchive = new List<string>();
                    for (int i = 0; i < MaxFilesPerCategory; i++)
                    {
                        var fileName = GetFileName(i);
                        if (File.Exists(fileName))
                        {
                            filesToArchive.Add(fileName);
                        }
                    }

                    // Si on a 3 fichiers, créer un ZIP
                    if (filesToArchive.Count >= MaxFilesPerCategory)
                    {
                        CreateArchive(filesToArchive);
                    }

                    // Rotation : décaler les fichiers
                    // Fichier 2 -> Fichier 3 (supprimer)
                    var file2 = GetFileName(2);
                    if (File.Exists(file2))
                    {
                        File.Delete(file2);
                    }

                    // Fichier 1 -> Fichier 2
                    var file1 = GetFileName(1);
                    var file2New = GetFileName(2);
                    if (File.Exists(file1))
                    {
                        File.Move(file1, file2New);
                    }

                    // Fichier 0 -> Fichier 1
                    var file0 = GetFileName(0);
                    var file1New = GetFileName(1);
                    if (File.Exists(file0))
                    {
                        File.Move(file0, file1New);
                    }

                    // Créer un nouveau fichier 0
                    _currentFileIndex = 0;
                    _currentFile = GetFileName(0);
                    _currentWriter = new StreamWriter(_currentFile, append: false, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };

                    // Ne pas logger ici pour éviter les références circulaires
                    System.Diagnostics.Debug.WriteLine($"[CategoryLogger] Rotation effectuée pour la catégorie {_category}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERREUR ROTATION: {ex.Message}");
                    // En cas d'erreur, réinitialiser le fichier actuel
                    InitializeCurrentFile();
                }
            }

            /// <summary>
            /// Crée une archive ZIP contenant les fichiers de log
            /// </summary>
            private void CreateArchive(List<string> filesToArchive)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var zipFileName = Path.Combine(_categoryDirectory, $"{_category}_{timestamp}{ZipExtension}");

                    using (var zipStream = new FileStream(zipFileName, FileMode.Create))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    {
                        foreach (var file in filesToArchive)
                        {
                            try
                            {
                                var entryName = Path.GetFileName(file);
                                archive.CreateEntryFromFile(file, entryName);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"ERREUR AJOUT FICHIER DANS ZIP: {ex.Message}");
                            }
                        }
                    }

                    // Supprimer les fichiers originaux après archivage
                    foreach (var file in filesToArchive)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }

                    // Ne pas logger ici pour éviter les références circulaires
                    System.Diagnostics.Debug.WriteLine($"[CategoryLogger] Archive créée: {zipFileName} ({filesToArchive.Count} fichiers)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERREUR CRÉATION ZIP: {ex.Message}");
                }
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    try
                    {
                        _currentWriter?.Flush();
                        _currentWriter?.Close();
                        _currentWriter?.Dispose();
                        _currentWriter = null;
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Niveaux de log
        /// </summary>
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Critical
        }

        /// <summary>
        /// Catégories prédéfinies pour organiser les logs
        /// </summary>
        public static class Categories
        {
            public const string Application = "APPLICATION";
            public const string System = "SYSTEM";
            public const string Kikimeter = "KIKIMETER";
            public const string Loot = "LOOT";
            public const string SaleTracker = "SALE_TRACKER";
            public const string Configuration = "CONFIGURATION";
            public const string Network = "NETWORK";
            public const string UI = "UI";
            public const string Performance = "PERFORMANCE";
            public const string Error = "ERROR";
        }
    }
}

