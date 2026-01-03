using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameOverlay.Kikimeter.Models;

/// <summary>
/// Représente un item ramassé par un personnage
/// </summary>
public class LootItem : INotifyPropertyChanged
{
    private int _quantity;
    private bool _isFavorite;
    
    /// <summary>
    /// Nom du personnage qui a ramassé l'item ("Vous" pour le perso principal, ou le pseudo)
    /// </summary>
    public string CharacterName { get; }
    
    /// <summary>
    /// Nom de l'item
    /// </summary>
    public string ItemName { get; }
    
    /// <summary>
    /// Indique si l'item est en favoris
    /// </summary>
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            _isFavorite = value;
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// Quantité totale ramassée
    /// </summary>
    public int Quantity 
    { 
        get => _quantity;
        set 
        { 
            _quantity = value; 
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// Heure du premier ramassage
    /// </summary>
    public DateTime FirstObtained { get; }
    
    /// <summary>
    /// Heure du dernier ramassage
    /// </summary>
    public DateTime LastObtained 
    { 
        get => _lastObtained;
        set 
        { 
            _lastObtained = value; 
            OnPropertyChanged();
        }
    }
    private DateTime _lastObtained;
    
    public LootItem(string characterName, string itemName, int quantity)
    {
        CharacterName = characterName;
        ItemName = itemName;
        Quantity = quantity;
        FirstObtained = DateTime.Now;
        LastObtained = DateTime.Now;
    }
    
    /// <summary>
    /// Ajoute une quantité à cet item
    /// </summary>
    public void AddQuantity(int additionalQuantity)
    {
        Quantity += additionalQuantity;
        LastObtained = DateTime.Now;
    }
    
    /// <summary>
    /// Retire une quantité de cet item (retourne true si la quantité atteint 0 ou moins)
    /// </summary>
    public bool RemoveQuantity(int quantityToRemove)
    {
        Quantity = Math.Max(0, Quantity - quantityToRemove);
        LastObtained = DateTime.Now;
        return Quantity <= 0;
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}


