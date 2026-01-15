using System;
using System.IO;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Models;
using Newtonsoft.Json;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service d'initialisation automatique du fichier player_data.json
/// Crée le fichier s'il n'existe pas, basé sur l'exemple
/// </summary>
public static class PlayerDataJsonInitializer
{
    private const string LogCategory = "PlayerDataJsonInitializer";
    private const string JsonFileName = "player_data.json";
    private const string ExampleFileName = "player_data.example.json";

    /// <summary>
    /// Initialise le fichier player_data.json s'il n'existe pas
    /// Ne bloque jamais le démarrage si le fichier est manquant ou inaccessible
    /// </summary>
    /// <returns>true si le fichier existe ou a été créé, false sinon</returns>
    public static bool EnsurePlayerDataJsonExists()
    {
        try
        {
            // Déterminer le chemin du fichier JSON
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant",
                "Kikimeter"
            );

            var jsonFilePath = Path.Combine(appDataDir, JsonFileName);

            // Si le fichier existe déjà, vérifier qu'il est valide
            if (File.Exists(jsonFilePath))
            {
                try
                {
                    var content = File.ReadAllText(jsonFilePath);
                    var data = JsonConvert.DeserializeObject<PlayerDataJson>(content);
                    if (data != null)
                    {
                        Logger.Debug(LogCategory, $"Fichier player_data.json existe et est valide: {jsonFilePath}");
                        return true;
                    }
                    else
                    {
                        Logger.Warning(LogCategory, $"Fichier player_data.json existe mais est invalide, recréation...");
                        // Le fichier est invalide, on va le recréer
                    }
                }
                catch (JsonException ex)
                {
                    Logger.Warning(LogCategory, $"Fichier player_data.json corrompu ({ex.Message}), recréation...");
                    // Le fichier est corrompu, on va le recréer
                }
                catch (Exception ex)
                {
                    Logger.Warning(LogCategory, $"Erreur lors de la lecture de player_data.json ({ex.Message}), recréation...");
                    // Erreur de lecture, on va le recréer
                }
            }

            // Créer le dossier si nécessaire
            if (!Directory.Exists(appDataDir))
            {
                try
                {
                    Directory.CreateDirectory(appDataDir);
                    Logger.Info(LogCategory, $"Dossier créé: {appDataDir}");
                }
                catch (Exception ex)
                {
                    Logger.Error(LogCategory, $"Impossible de créer le dossier {appDataDir}: {ex.Message}");
                    return false; // Ne peut pas créer le dossier, abandon
                }
            }

            // Créer le fichier JSON initial
            var initialData = CreateInitialPlayerDataJson();

            try
            {
                var json = JsonConvert.SerializeObject(initialData, Formatting.Indented);
                File.WriteAllText(jsonFilePath, json, System.Text.Encoding.UTF8);
                Logger.Info(LogCategory, $"Fichier player_data.json créé avec succès: {jsonFilePath}");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error(LogCategory, $"Accès refusé lors de la création de player_data.json: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Logger.Error(LogCategory, $"Erreur I/O lors de la création de player_data.json: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(LogCategory, $"Erreur inattendue lors de la création de player_data.json: {ex.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            // Ne jamais bloquer le démarrage
            Logger.Error(LogCategory, $"Erreur lors de l'initialisation de player_data.json: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Crée un objet PlayerDataJson initial vide mais valide
    /// </summary>
    private static PlayerDataJson CreateInitialPlayerDataJson()
    {
        return new PlayerDataJson
        {
            Players = new System.Collections.Generic.List<PlayerDataItem>(),
            CombatActive = false,
            LastUpdate = DateTime.Now,
            ServerName = null
        };
    }

    /// <summary>
    /// Vérifie si le fichier JSON existe et est accessible
    /// </summary>
    public static bool IsPlayerDataJsonAccessible()
    {
        try
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Amaliassistant",
                "Kikimeter"
            );

            var jsonFilePath = Path.Combine(appDataDir, JsonFileName);

            if (!File.Exists(jsonFilePath))
            {
                return false;
            }

            // Tenter de lire le fichier pour vérifier l'accessibilité
            var content = File.ReadAllText(jsonFilePath);
            var data = JsonConvert.DeserializeObject<PlayerDataJson>(content);
            return data != null;
        }
        catch
        {
            return false;
        }
    }
}
