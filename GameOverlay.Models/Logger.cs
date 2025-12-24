using System;
using System.IO;
using System.Diagnostics;

namespace GameOverlay.Models
{
    /// <summary>
    /// Logger global pour diagnostiquer les freezes et crashes
    /// Utilise AdvancedLogger en arrière-plan pour la rotation de fichiers et l'archivage
    /// </summary>
    public static class Logger
    {
        private const string DefaultCategory = "APPLICATION";

        /// <summary>
        /// Marque un heartbeat pour indiquer que le système est vivant
        /// </summary>
        public static void Heartbeat(string module)
        {
            AdvancedLogger.Heartbeat(DefaultCategory, module);
        }

        /// <summary>
        /// Ferme le logger proprement
        /// </summary>
        public static void Close()
        {
            AdvancedLogger.Close();
        }

        public static void LogDebug(string module, string message)
        {
            AdvancedLogger.Debug(DefaultCategory, module, message);
        }

        public static void Info(string module, string message)
        {
            AdvancedLogger.Info(DefaultCategory, module, message);
        }

        public static void Warning(string module, string message)
        {
            AdvancedLogger.Warning(DefaultCategory, module, message);
        }

        public static void Error(string module, string message)
        {
            AdvancedLogger.Error(DefaultCategory, module, message);
        }

        public static void Error(string module, string message, Exception ex)
        {
            AdvancedLogger.Error(DefaultCategory, module, message, ex);
        }

        public static void Critical(string module, string message)
        {
            AdvancedLogger.Critical(DefaultCategory, module, message);
        }

        public static void Critical(string module, string message, Exception ex)
        {
            AdvancedLogger.Critical(DefaultCategory, module, message, ex);
        }

        /// <summary>
        /// Log une opération avec mesure de temps
        /// </summary>
        public static IDisposable Measure(string module, string operation)
        {
            return AdvancedLogger.Measure(DefaultCategory, module, operation);
        }
        
        /// <summary>
        /// Alias pour compatibilité
        /// </summary>
        public static void Debug(string module, string message) => LogDebug(module, message);
    }
}





