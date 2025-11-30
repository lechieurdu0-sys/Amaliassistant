using System;
using System.IO;
using System.Diagnostics;

namespace GameOverlay.Models
{
    /// <summary>
    /// Logger global pour diagnostiquer les freezes et crashes
    /// </summary>
    public static class Logger
    {
        private static string _logDirectory = "";
        private static string _logFile = "";
        private static readonly object _lock = new object();
        private static StreamWriter? _logWriter;
        private static System.Threading.Timer? _heartbeatTimer;
        private static DateTime _lastHeartbeat = DateTime.Now;

        static Logger()
        {
            try
            {
                _logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Amaliassistant",
                    "logs"
                );

                Directory.CreateDirectory(_logDirectory);

                _logFile = Path.Combine(_logDirectory, $"app_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                // Créer le fichier de log
                _logWriter = new StreamWriter(_logFile, append: true)
                {
                    AutoFlush = true
                };

                LogDebug("Logger", "Logger initialisé");
                
                // Démarrer le heartbeat pour détecter les freezes
                _heartbeatTimer = new System.Threading.Timer(HeartbeatCallback, null, 5000, 5000);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERREUR INIT LOGGER: {ex.Message}");
            }
        }
        
        private static void HeartbeatCallback(object? state)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastHeartbeat).TotalSeconds;
            
            if (elapsed > 10)
            {
                Critical("Heartbeat", $"⚠️ FREEZE DÉTECTÉ ! Dernier heartbeat il y a {elapsed:F1} secondes");
            }
            
            _lastHeartbeat = now;
        }
        
        /// <summary>
        /// Marque un heartbeat pour indiquer que le système est vivant
        /// </summary>
        public static void Heartbeat(string module)
        {
            _lastHeartbeat = DateTime.Now;
        }

        /// <summary>
        /// Ferme le logger proprement
        /// </summary>
        public static void Close()
        {
            lock (_lock)
            {
                try
                {
                    _heartbeatTimer?.Dispose();
                    _heartbeatTimer = null;
                    _logWriter?.Flush();
                    _logWriter?.Close();
                    _logWriter?.Dispose();
                    _logWriter = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERREUR FERMETURE LOGGER: {ex.Message}");
                }
            }
        }

        private static void WriteLog(string level, string module, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] [{module}] {message}";

                lock (_lock)
                {
                    System.Diagnostics.Debug.WriteLine(logEntry);
                    _logWriter?.WriteLine(logEntry);
                    _logWriter?.Flush();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERREUR ÉCRITURE LOG: {ex.Message}");
            }
        }

        public static void LogDebug(string module, string message)
        {
            WriteLog("DEBUG", module, message);
        }

        public static void Info(string module, string message)
        {
            WriteLog("INFO", module, message);
        }

        public static void Warning(string module, string message)
        {
            WriteLog("WARN", module, message);
        }

        public static void Error(string module, string message)
        {
            WriteLog("ERROR", module, message);
        }

        public static void Error(string module, string message, Exception ex)
        {
            WriteLog("ERROR", module, $"{message} | Exception: {ex.Message} | StackTrace: {ex.StackTrace}");
        }

        public static void Critical(string module, string message)
        {
            WriteLog("CRITICAL", module, message);
        }

        public static void Critical(string module, string message, Exception ex)
        {
            WriteLog("CRITICAL", module, $"{message} | Exception: {ex.Message} | StackTrace: {ex.StackTrace}");
        }

        /// <summary>
        /// Log une opération avec mesure de temps
        /// </summary>
        public static IDisposable Measure(string module, string operation)
        {
            var sw = Stopwatch.StartNew();
            Info(module, $"START: {operation}");
            return new TimingLogger(module, operation, sw);
        }
        
        /// <summary>
        /// Alias pour compatibilité
        /// </summary>
        public static void Debug(string module, string message) => LogDebug(module, message);

        private class TimingLogger : IDisposable
        {
            private readonly string _module;
            private readonly string _operation;
            private readonly Stopwatch _sw;

            public TimingLogger(string module, string operation, Stopwatch sw)
            {
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
                    Logger.Warning(_module, $"END (SLOW): {_operation} - {elapsed}ms");
                }
                else
                {
                    Logger.Info(_module, $"END: {_operation} - {elapsed}ms");
                }
            }
        }
    }
}





