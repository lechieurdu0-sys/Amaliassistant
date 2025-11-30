using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service pour localiser automatiquement les fichiers de logs Wakfu
/// </summary>
public static class WakfuLogFinder
{
    /// <summary>
    /// Chemins standards pour les logs Wakfu
    /// </summary>
    private static readonly string[] StandardPaths = new[]
    {
        // Steam - Fichier principal (contient les infos de combat et invocations)
        Path.Combine("SteamLibrary", "steamapps", "common", "Wakfu", "preferences", "logs", "wakfu.log"),
        // Ankama Launcher (Zaap) - Fichier principal
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zaap", "gamesLogs", "wakfu", "logs", "wakfu.log"),
        // Fallback dans AppData
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wakfu", "logs", "wakfu.log"),
    };

    /// <summary>
    /// Trouve tous les fichiers de logs Wakfu valides
    /// </summary>
    /// <returns>Liste des chemins absolus valides</returns>
    public static List<string> FindAllLogFiles()
    {
        var foundFiles = new List<string>();

        // Vérifier les chemins standards
        foreach (var relativePath in StandardPaths)
        {
            var fullPath = Path.IsPathRooted(relativePath) 
                ? relativePath 
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), relativePath);
            
            if (File.Exists(fullPath))
            {
                foundFiles.Add(fullPath);
            }
        }

        // Chercher dans tous les lecteurs disponibles
        foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            string driveRoot = drive.RootDirectory.FullName;
            
            // Chemins Steam potentiels
            string steamPath = Path.Combine(driveRoot, "SteamLibrary", "steamapps", "common", "Wakfu", "preferences", "logs", "wakfu.log");
            if (File.Exists(steamPath))
            {
                foundFiles.Add(steamPath);
            }
            
            string steamAltPath = Path.Combine(driveRoot, "Program Files (x86)", "Steam", "steamapps", "common", "Wakfu", "preferences", "logs", "wakfu.log");
            if (File.Exists(steamAltPath))
            {
                foundFiles.Add(steamAltPath);
            }
            
            // Chemins Ankama Launcher (si installé ailleurs que dans AppData)
            string ankamaPath = Path.Combine(driveRoot, "Program Files (x86)", "Ankama", "Zaap", "gamesLogs", "wakfu", "logs", "wakfu.log");
            if (File.Exists(ankamaPath))
            {
                foundFiles.Add(ankamaPath);
            }
        }

        // Chercher dans Program Files
        var programFilesPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Wakfu", "preferences", "logs", "wakfu.log"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Wakfu", "preferences", "logs", "wakfu.log")
        };

        foreach (var path in programFilesPaths)
        {
            if (File.Exists(path))
            {
                foundFiles.Add(path);
            }
        }

        return foundFiles.Distinct().ToList();
    }

    /// <summary>
    /// Trouve le premier fichier de log valide ou retourne le chemin par défaut
    /// </summary>
    /// <param name="defaultPath">Chemin par défaut si aucun fichier trouvé</param>
    /// <returns>Chemin du fichier de log ou chemin par défaut</returns>
    public static string FindFirstLogFile(string? defaultPath = null)
    {
        var files = FindAllLogFiles();
        return files.FirstOrDefault() ?? defaultPath ?? string.Empty;
    }

    /// <summary>
    /// Obtient un nom d'affichage lisible pour un chemin de log
    /// </summary>
    /// <param name="logPath">Chemin complet du fichier</param>
    /// <returns>Nom d'affichage simplifié</returns>
    public static string GetDisplayName(string logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
            return "Non spécifié";

        // Simplifier le chemin pour l'affichage
        if (logPath.Contains("SteamLibrary"))
            return "📦 Steam";
        
        if (logPath.Contains("zaap"))
            return "🎮 Ankama Launcher";
        
        if (logPath.Contains("AppData"))
            return "📁 AppData";
        
        return Path.GetFileName(logPath);
    }
    
    /// <summary>
    /// Trouve le fichier wakfu_chat.log correspondant à un wakfu.log
    /// </summary>
    /// <param name="wakfuLogPath">Chemin vers wakfu.log</param>
    /// <returns>Chemin vers wakfu_chat.log ou string.Empty si non trouvé</returns>
    public static string FindChatLogFile(string wakfuLogPath)
    {
        if (string.IsNullOrWhiteSpace(wakfuLogPath))
            return string.Empty;
        
        // Remplacer "wakfu.log" par "wakfu_chat.log" dans le chemin
        string chatLogPath = wakfuLogPath.Replace("wakfu.log", "wakfu_chat.log");
        
        if (File.Exists(chatLogPath))
        {
            return chatLogPath;
        }
        
        return string.Empty;
    }
}

