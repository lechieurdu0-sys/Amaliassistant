namespace GameOverlay.Kikimeter.Models;

/// <summary>
/// État du cycle de vie du Kikimeter
/// </summary>
public enum CombatState
{
    /// <summary>
    /// En attente de détection d'un combat
    /// </summary>
    Waiting,
    
    /// <summary>
    /// Combat actif en cours
    /// </summary>
    Active,
    
    /// <summary>
    /// Combat terminé, affichage figé
    /// </summary>
    Terminated
}

