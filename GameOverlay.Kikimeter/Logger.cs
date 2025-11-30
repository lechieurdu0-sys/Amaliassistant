using System;
using System.IO;
using System.Linq;

namespace GameOverlay.Kikimeter;

public static class Logger
{
    private const int MaxLogFiles = 20;
    private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static readonly object LockObject = new object();
    private static readonly string CurrentLogFile;

    static Logger()
    {
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }

        CurrentLogFile = Path.Combine(LogDirectory, $"app_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        PruneOldLogs();
    }

    private static void PruneOldLogs()
    {
        try
        {
            var files = Directory.GetFiles(LogDirectory, "app_*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            if (files.Count <= MaxLogFiles)
                return;

            foreach (var file in files.Skip(MaxLogFiles))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignorer les erreurs de suppression (fichier verrouillé, etc.)
                }
            }
        }
        catch
        {
            // Ignorer les erreurs pendant le nettoyage
        }
    }

    public static void Log(string level, string category, string message)
    {
        lock (LockObject)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [{level}] [{category}] {message}";

            try
            {
                File.AppendAllText(CurrentLogFile, logMessage + Environment.NewLine);
            }
            catch { }
        }
    }

    public static void Debug(string category, string message) => Log("DEBUG", category, message);
    public static void Info(string category, string message) => Log("INFO", category, message);
    public static void Warning(string category, string message) => Log("WARNING", category, message);
    public static void Error(string category, string message) => Log("ERROR", category, message);
}

