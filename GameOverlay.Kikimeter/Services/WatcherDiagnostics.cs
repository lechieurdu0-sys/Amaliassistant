using System;
using System.Diagnostics;
using System.IO;

namespace GameOverlay.Kikimeter.Services;

internal static class WatcherDiagnostics
{
    private static readonly object FileLock = new();
    private static readonly string DiagnosticsDirectory =
        Path.Combine(AppContext.BaseDirectory, "logs", "diagnostics");

    private static readonly string DiagnosticsFilePath =
        Path.Combine(DiagnosticsDirectory, "watcher_diagnostics.log");

    public static void Log(string message)
    {
        try
        {
            var timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

            lock (FileLock)
            {
                Directory.CreateDirectory(DiagnosticsDirectory);
                File.AppendAllText(DiagnosticsFilePath, timestamped + Environment.NewLine);
            }
        }
        catch
        {
            // Ne rien faire : la journalisation de diagnostic ne doit jamais faire planter l'application.
        }
    }

    public static void LogStopWatching(string? logPath)
    {
        var stack = new StackTrace(true);
        Log($"StopWatching invoqué pour '{logPath ?? "chemin inconnu"}'. Stack :{Environment.NewLine}{stack}");
    }

    public static void LogStartWatching(string? logPath)
    {
        Log($"StartWatching invoqué pour '{logPath ?? "chemin inconnu"}'.");
    }

    public static void LogFileEvent(string eventType, string details)
    {
        Log($"[{eventType}] {details}");
    }

    public static void LogReadStatus(string phase, long lastPosition, long fileLength)
    {
        Log($"[{phase}] lastPosition={lastPosition}, fileLength={fileLength}");
    }
}


