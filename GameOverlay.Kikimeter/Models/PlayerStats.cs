using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using GameOverlay.Kikimeter.Services;

namespace GameOverlay.Kikimeter.Models;

/// <summary>
/// Statistiques d'un joueur pendant un combat
/// </summary>
public class PlayerStats : INotifyPropertyChanged
{
    public const long MaxStatValue = 999_999_999;

    public static double MapValue(long value) => Math.Log10(value + 1);
    public static double MappedMaxValue => MapValue(MaxStatValue);

    private long _damageDealt;
    private long _damageTaken;
    private long _healingDone;
    private long _shieldGiven;
    private int _numberOfTurns;
    private long _damageThisTurn;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayNameWithClass));
            }
        }
    }

    private string _breed = string.Empty;
    public string Breed
    {
        get => _breed;
        set
        {
            var normalized = value ?? string.Empty;
            if (_breed == normalized)
            {
                return;
            }

            _breed = normalized;
            OnPropertyChanged();

            // Mettre à jour l'icône quand le breed change
            UpdateClassResources();

            if (string.IsNullOrEmpty(_className))
            {
                var classFromBreed = BreedClassMapper.GetClassName(normalized);
                if (!string.IsNullOrEmpty(classFromBreed))
                {
                    ClassName = classFromBreed;
                }
            }
        }
    }

    private string _className = string.Empty;
    private string? _classIconUri;
    private ImageSource? _classIconImage;

    public string ClassName
    {
        get => _className;
        set
        {
            var normalized = value ?? string.Empty;
            if (_className == normalized)
            {
                return;
            }

            _className = normalized;
            UpdateClassResources();
        }
    }

    public string? ClassIconUri
    {
        get => _classIconUri;
        private set
        {
            if (!string.Equals(_classIconUri, value, StringComparison.Ordinal))
            {
                _classIconUri = value;
                OnPropertyChanged();
            }
        }
    }

    public ImageSource? ClassIconImage
    {
        get => _classIconImage;
        private set
        {
            if (!ReferenceEquals(_classIconImage, value))
            {
                _classIconImage = value;
                OnPropertyChanged();
            }
        }
    }

    public string ClassDisplayName => ClassResourceProvider.GetDisplayName(ClassName);
    public string ClassDisplaySuffix => string.IsNullOrEmpty(ClassDisplayName) ? string.Empty : $" ({ClassDisplayName})";
    public string DisplayNameWithClass => string.IsNullOrEmpty(ClassDisplayName) ? Name : $"{Name} ({ClassDisplayName})";

    public long PlayerId { get; set; }

    public long DamageDealt
    {
        get => _damageDealt;
        set
        {
            _damageDealt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DamagePerTurn));
            OnPropertyChanged(nameof(DamageDealtMapped));
            OnPropertyChanged(nameof(DamageDealtRatio));
        }
    }

    public long DamageTaken
    {
        get => _damageTaken;
        set
        {
            _damageTaken = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DamageTakenMapped));
            OnPropertyChanged(nameof(DamageTakenRatio));
        }
    }

    public long HealingDone
    {
        get => _healingDone;
        set
        {
            _healingDone = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HealingDoneMapped));
            OnPropertyChanged(nameof(HealingDoneRatio));
        }
    }

    public long ShieldGiven
    {
        get => _shieldGiven;
        set
        {
            _shieldGiven = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShieldGivenMapped));
            OnPropertyChanged(nameof(ShieldGivenRatio));
        }
    }

    public long DamageBySummon { get; set; }
    public long DamageByAOE { get; set; }

    public int NumberOfTurns
    {
        get => _numberOfTurns;
        set
        {
            _numberOfTurns = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NumberOfTurnsMapped));
        }
    }

    public long DamageThisTurn
    {
        get => _damageThisTurn;
        set
        {
            _damageThisTurn = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DamagePerTurn));
            OnPropertyChanged(nameof(DamagePerTurnMapped));
        }
    }

    public long DamagePerTurn => _damageThisTurn;

    public double DamageDealtMapped => MapValue(_damageDealt);
    public double DamageTakenMapped => MapValue(_damageTaken);
    public double HealingDoneMapped => MapValue(_healingDone);
    public double ShieldGivenMapped => MapValue(_shieldGiven);
    public double DamagePerTurnMapped => MapValue(_damageThisTurn);
    public double NumberOfTurnsMapped => MapValue(_numberOfTurns);

    public double DamageDealtRatio => DamageDealtMapped / MappedMaxValue;
    public double DamageTakenRatio => DamageTakenMapped / MappedMaxValue;
    public double HealingDoneRatio => HealingDoneMapped / MappedMaxValue;
    public double ShieldGivenRatio => ShieldGivenMapped / MappedMaxValue;

    public void ResetTurnDamage()
    {
        _damageThisTurn = 0;
        OnPropertyChanged(nameof(DamageThisTurn));
        OnPropertyChanged(nameof(DamagePerTurn));
        OnPropertyChanged(nameof(DamagePerTurnMapped));
    }

    private int _manualOrder;
    public int ManualOrder
    {
        get => _manualOrder;
        set
        {
            if (_manualOrder != value)
            {
                _manualOrder = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isFirst;
    public bool IsFirst
    {
        get => _isFirst;
        set
        {
            if (_isFirst != value)
            {
                _isFirst = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isInGroup;
    /// <summary>
    /// Indique si le joueur fait partie du groupe (maximum 6 joueurs)
    /// </summary>
    public bool IsInGroup
    {
        get => _isInGroup;
        set
        {
            if (_isInGroup != value)
            {
                _isInGroup = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isMainCharacter;
    /// <summary>
    /// Indique si ce joueur est le personnage principal du joueur
    /// </summary>
    public bool IsMainCharacter
    {
        get => _isMainCharacter;
        set
        {
            if (_isMainCharacter != value)
            {
                _isMainCharacter = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isActive = true;
    /// <summary>
    /// Indique si le joueur est actuellement actif (présent dans le combat ou dans le groupe)
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime _lastSeenInCombat = DateTime.MinValue;
    /// <summary>
    /// Dernière fois que le joueur a été vu dans un combat
    /// Utilisé pour déterminer si un joueur doit être retiré après la fin d'un combat
    /// </summary>
    public DateTime LastSeenInCombat
    {
        get => _lastSeenInCombat;
        set
        {
            if (_lastSeenInCombat != value)
            {
                _lastSeenInCombat = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateClassResources()
    {
        OnPropertyChanged(nameof(ClassName));
        OnPropertyChanged(nameof(ClassDisplayName));
        OnPropertyChanged(nameof(ClassDisplaySuffix));
        OnPropertyChanged(nameof(DisplayNameWithClass));

        // Charger l'icône directement depuis le breed si disponible, sinon depuis le nom de classe
        if (!string.IsNullOrEmpty(_breed))
        {
            ClassIconImage = ClassResourceProvider.GetIconImageFromBreed(_breed);
        }
        else
        {
        ClassIconUri = ClassResourceProvider.GetIconUri(_className);
        ClassIconImage = ClassResourceProvider.GetIconImage(_className);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

