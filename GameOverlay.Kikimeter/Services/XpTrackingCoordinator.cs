using System;
using System.Collections.Generic;
using GameOverlay.XpTracker.Models;
using GameOverlay.XpTracker.Services;

namespace GameOverlay.Kikimeter.Services;

internal sealed class XpTrackingCoordinator : IDisposable
{
    private readonly LogFileWatcher _logFileWatcher;
    private readonly XpTrackerService _trackerService = new();
    private bool _isSubscribed;
    private bool _disposed;

    public event EventHandler<XpGainEvent>? ExperienceGained;

    public XpTrackingCoordinator(LogFileWatcher logFileWatcher)
    {
        _logFileWatcher = logFileWatcher ?? throw new ArgumentNullException(nameof(logFileWatcher));
        Subscribe();
    }

    public IReadOnlyCollection<XpTrackerEntry> GetEntries() => _trackerService.GetAllEntries();

    public XpTrackerEntry? GetEntry(string entityName) => _trackerService.GetEntry(entityName);

    public void Reset() => _trackerService.Reset();

    public void Reset(string entityName) => _trackerService.Reset(entityName);

    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed = true;
        _logFileWatcher.LogLineProcessed += OnLogLineProcessed;
        _trackerService.ExperienceGained += OnExperienceGained;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed = false;
        _logFileWatcher.LogLineProcessed -= OnLogLineProcessed;
        _trackerService.ExperienceGained -= OnExperienceGained;
    }

    private void OnLogLineProcessed(object? sender, string line)
    {
        try
        {
            _trackerService.TryProcessLine(line, out _);
        }
        catch (Exception ex)
        {
            Logger.Warning("XpTrackingCoordinator", $"Erreur lors du traitement d'une ligne XP: {ex.Message}");
        }
    }

    private void OnExperienceGained(object? sender, XpGainEvent e)
    {
        ExperienceGained?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Unsubscribe();
    }
}


