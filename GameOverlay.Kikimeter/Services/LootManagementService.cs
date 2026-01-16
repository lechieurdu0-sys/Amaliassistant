using System;
using System.Collections.ObjectModel;
using System.Linq;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Models;

namespace GameOverlay.Kikimeter.Services;

/// <summary>
/// Service de gestion centralisé du loot - SOURCE UNIQUE DE VÉRITÉ
/// Gère la collection de loot pour toute la session de l'application
/// Ne se reset JAMAIS pendant la session (sauf reset explicite)
/// </summary>
public class LootManagementService
{
    private const string LogCategory = "LootManagementService";
    
    /// <summary>
    /// Collection centrale du loot - SOURCE UNIQUE DE VÉRITÉ pour toute la session
    /// Ne doit JAMAIS être recréée, reconstruite, ou vidée (sauf reset explicite)
    /// </summary>
    public ObservableCollection<LootItem> SessionLoot { get; } = new();
    
    /// <summary>
    /// HashSet pour suivre les items supprimés par l'utilisateur
    /// Un item supprimé ne doit JAMAIS réapparaître (sauf s'il est redrop dans le jeu)
    /// </summary>
    private readonly HashSet<string> _userDeletedKeys = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// HashSet pour suivre les favoris
    /// </summary>
    private readonly HashSet<string> _favoriteKeys = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Ajoute un loot ou incrémente la quantité si l'item existe déjà
    /// </summary>
    public void AddOrUpdateLoot(string characterName, string itemName, int quantity)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(itemName))
            return;
            
        var key = $"{characterName}|{itemName}";
        
        // Vérifier si l'item a été supprimé par l'utilisateur
        // Si oui, le permettre seulement s'il est redrop dans le jeu (nouvelle entrée de log)
        // Dans ce cas, on considère que c'est un nouveau drop et on retire de la liste des supprimés
        if (_userDeletedKeys.Contains(key))
        {
            // C'est un redrop après suppression - permettre l'ajout
            _userDeletedKeys.Remove(key);
            Logger.Info(LogCategory, $"Loot redrop après suppression : {characterName}: {itemName} x{quantity}");
        }
        
        // Rechercher l'item existant
        var existing = SessionLoot.FirstOrDefault(i =>
            string.Equals(i.CharacterName, characterName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
        {
            // Item existant : incrémenter la quantité
            existing.AddQuantity(quantity);
            Logger.Info(LogCategory, $"Loot incrémenté : {characterName}: {itemName} → Quantité {existing.Quantity}");
        }
        else
        {
            // Nouvel item : créer et ajouter
            var newItem = new LootItem(characterName, itemName, quantity);
            
            // Appliquer l'état favoris si l'item était favori (pour les redrops)
            if (_favoriteKeys.Contains(key))
            {
                newItem.IsFavorite = true;
            }
            
            SessionLoot.Add(newItem);
            Logger.Info(LogCategory, $"Loot ajouté : {characterName}: {itemName} x{quantity}");
        }
    }
    
    /// <summary>
    /// Retire une quantité d'un loot (appelé par le parser pour les retraits/destructions dans le jeu)
    /// </summary>
    public void RemoveLootQuantity(string characterName, string itemName, int quantity)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(itemName))
            return;
            
        var key = $"{characterName}|{itemName}";
        var existing = SessionLoot.FirstOrDefault(i =>
            string.Equals(i.CharacterName, characterName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
        {
            bool shouldRemove = existing.RemoveQuantity(quantity);
            
            // Ne retirer de la collection que si quantité <= 0 ET pas favori
            if (shouldRemove && !existing.IsFavorite)
            {
                SessionLoot.Remove(existing);
                Logger.Info(LogCategory, $"Loot retiré (quantité 0) : {characterName}: {itemName}");
            }
            else if (shouldRemove && existing.IsFavorite)
            {
                // Garder l'item favori même si quantité = 0
                Logger.Info(LogCategory, $"Loot favori conservé (quantité 0) : {characterName}: {itemName}");
            }
            else
            {
                Logger.Debug(LogCategory, $"Loot quantité mise à jour : {characterName}: {itemName} → Quantité {existing.Quantity}");
            }
        }
        else
        {
            Logger.Debug(LogCategory, $"Tentative de retrait d'un item non trouvé : {characterName}: {itemName}");
        }
    }
    
    /// <summary>
    /// Supprime un loot par l'utilisateur (clic sur le bouton de suppression)
    /// Un item supprimé ne doit JAMAIS réapparaître (sauf s'il est redrop dans le jeu)
    /// </summary>
    public bool DeleteLootByUser(string characterName, string itemName)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(itemName))
            return false;
            
        var key = $"{characterName}|{itemName}";
        var existing = SessionLoot.FirstOrDefault(i =>
            string.Equals(i.CharacterName, characterName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
        
        if (existing == null)
            return false;
            
        // Ne pas supprimer si l'item est favori
        if (existing.IsFavorite)
        {
            Logger.Info(LogCategory, $"Suppression refusée (favori) : {characterName}: {itemName}");
            return false;
        }
        
        // Retirer de la collection
        SessionLoot.Remove(existing);
        
        // Ajouter à la liste des items supprimés par l'utilisateur
        _userDeletedKeys.Add(key);
        
        Logger.Info(LogCategory, $"Loot supprimé par utilisateur : {characterName}: {itemName}");
        return true;
    }
    
    /// <summary>
    /// Marque un item comme favori ou retire le favori
    /// </summary>
    public void ToggleFavorite(string characterName, string itemName)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(itemName))
            return;
            
        var key = $"{characterName}|{itemName}";
        var existing = SessionLoot.FirstOrDefault(i =>
            string.Equals(i.CharacterName, characterName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
        
        if (existing == null)
            return;
            
        if (existing.IsFavorite)
        {
            // Retirer le favori
            _favoriteKeys.Remove(key);
            existing.IsFavorite = false;
            Logger.Info(LogCategory, $"Favori retiré : {characterName}: {itemName}");
            
            // Si la quantité est 0, supprimer l'item
            if (existing.Quantity == 0)
            {
                SessionLoot.Remove(existing);
                Logger.Info(LogCategory, $"Item favori supprimé (quantité 0, favori retiré) : {characterName}: {itemName}");
            }
        }
        else
        {
            // Ajouter le favori
            _favoriteKeys.Add(key);
            existing.IsFavorite = true;
            Logger.Info(LogCategory, $"Favori ajouté : {characterName}: {itemName}");
        }
    }
    
    /// <summary>
    /// Vérifie si un item est dans la liste des supprimés par l'utilisateur
    /// </summary>
    public bool IsUserDeleted(string characterName, string itemName)
    {
        var key = $"{characterName}|{itemName}";
        return _userDeletedKeys.Contains(key);
    }
    
    /// <summary>
    /// Reset complet (uniquement lors d'un reset serveur ou fermeture d'application)
    /// </summary>
    public void Reset()
    {
        SessionLoot.Clear();
        _userDeletedKeys.Clear();
        // Ne PAS vider _favoriteKeys - les favoris persistent même après reset
        Logger.Info(LogCategory, "Reset complet du service de loot");
    }
    
    /// <summary>
    /// Charge les favoris depuis le stockage persistant
    /// </summary>
    public void LoadFavorites(HashSet<string> favoriteKeys)
    {
        if (favoriteKeys == null)
            return;
            
        _favoriteKeys.Clear();
        foreach (var key in favoriteKeys)
        {
            _favoriteKeys.Add(key);
        }
        
        // Appliquer les favoris aux items existants
        foreach (var item in SessionLoot)
        {
            var key = $"{item.CharacterName}|{item.ItemName}";
            item.IsFavorite = _favoriteKeys.Contains(key);
        }
        
        Logger.Info(LogCategory, $"Favoris chargés : {_favoriteKeys.Count} favoris");
    }
    
    /// <summary>
    /// Sauvegarde les favoris dans le stockage persistant
    /// </summary>
    public HashSet<string> SaveFavorites()
    {
        return new HashSet<string>(_favoriteKeys, StringComparer.OrdinalIgnoreCase);
    }
}
