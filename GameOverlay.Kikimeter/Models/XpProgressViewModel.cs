using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using GameOverlay.XpTracker.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace GameOverlay.Kikimeter.Models;

public sealed class XpProgressViewModel : INotifyPropertyChanged
{
    private const string FallbackColorHex = "#FF00AEEF";
    private const long MaxExperienceValue = 999_999_999_999_999_999;
    private static readonly double MappedMaxExperience = MapValue(MaxExperienceValue);

    private long _totalExperience;
    private long _lastGain;
    private long? _experienceToNextLevel;
    private double _progressMaximum = MappedMaxExperience;
    private MediaBrush _progressBrush = CreateBrushFromHex(FallbackColorHex);
    private string _progressColorHex = FallbackColorHex;
    private DateTime _lastUpdate;

    public XpProgressViewModel(string entityName)
    {
        EntityName = entityName;
        ResetColorToTheme();
    }

    public string EntityName { get; }

    public long TotalExperience
    {
        get => _totalExperience;
        private set
        {
            if (_totalExperience != value)
            {
                _totalExperience = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(ProgressRatio));
                OnPropertyChanged(nameof(TotalExperienceDisplay));
            }
        }
    }

    public long LastGain
    {
        get => _lastGain;
        private set
        {
            if (_lastGain != value)
            {
                _lastGain = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastGainDisplay));
            }
        }
    }

    public long? ExperienceToNextLevel
    {
        get => _experienceToNextLevel;
        private set
        {
            if (_experienceToNextLevel != value)
            {
                _experienceToNextLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RemainingDisplay));
            }
        }
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set
        {
            if (Math.Abs(_progressMaximum - value) > double.Epsilon)
            {
                _progressMaximum = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressRatio));
            }
        }
    }

    public MediaBrush ProgressBrush
    {
        get => _progressBrush;
        private set
        {
            if (_progressBrush != value)
            {
                _progressBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public string ProgressColorHex
    {
        get => _progressColorHex;
        private set
        {
            if (!string.Equals(_progressColorHex, value, StringComparison.OrdinalIgnoreCase))
            {
                _progressColorHex = value;
                OnPropertyChanged();
            }
        }
    }

    public double ProgressValue => MapValue(TotalExperience);

    public double ProgressRatio
    {
        get
        {
            if (ProgressMaximum <= 0)
                return 0;

            return Math.Max(0, Math.Min(1.0, ProgressValue / ProgressMaximum));
        }
    }

    public string TotalExperienceDisplay => TotalExperience.ToString("N0");

    public string LastGainDisplay => LastGain > 0 ? $"+{LastGain:N0}" : "+0";

    public string RemainingDisplay => ExperienceToNextLevel.HasValue
        ? $"{ExperienceToNextLevel.Value:N0}"
        : "—";

    public DateTime LastUpdate
    {
        get => _lastUpdate;
        private set
        {
            if (_lastUpdate != value)
            {
                _lastUpdate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastUpdateDisplay));
            }
        }
    }

    public string LastUpdateDisplay => LastUpdate == default
        ? "—"
        : LastUpdate.ToString("HH:mm:ss");

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyEntry(XpTrackerEntry entry, XpGainEvent? xpEvent)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        ProgressMaximum = MappedMaxExperience;

        TotalExperience = entry.TotalExperienceGained;
        ExperienceToNextLevel = entry.ExperienceToNextLevel;
        LastUpdate = entry.LastUpdate;

        if (xpEvent != null)
        {
            LastGain = xpEvent.ExperienceGained;
        }
        else if (entry.EventCount == 0)
        {
            LastGain = 0;
        }

        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressRatio));
    }

    public void ApplyColor(string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            return;

        try
        {
            ProgressBrush = CreateBrushFromHex(colorHex);
            ProgressColorHex = colorHex;
        }
        catch
        {
            // Ignorer le format invalide
        }
    }

    public void ResetColorToTheme()
    {
        var defaultHex = GetDefaultThemeColorHex();
        _progressBrush = CreateBrushFromHex(defaultHex);
        _progressColorHex = defaultHex;
        OnPropertyChanged(nameof(ProgressBrush));
        OnPropertyChanged(nameof(ProgressColorHex));
    }

    public static string GetDefaultThemeColorHex()
    {
        var app = System.Windows.Application.Current;
        if (app != null)
        {
            if (app.TryFindResource("CyanAccentBrush") is SolidColorBrush brush)
            {
                var color = brush.Color;
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }

        return FallbackColorHex;
    }

    private static double MapValue(long value)
    {
        if (value <= 0)
            return 0d;

        return Math.Log10(value + 1d);
    }

    private static MediaBrush CreateBrushFromHex(string colorHex)
    {
        var color = ParseColor(colorHex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static MediaColor ParseColor(string colorHex)
    {
        var converted = MediaColorConverter.ConvertFromString(colorHex);
        if (converted is MediaColor mediaColor)
        {
            return mediaColor;
        }

        return MediaColor.FromArgb(0xFF, 0x00, 0xAE, 0xEF);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


