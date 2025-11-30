using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameOverlay.Models;
using Newtonsoft.Json;

namespace GameOverlay.Windows;

public static class BubbleDragHelper
{
    private static readonly Dictionary<UserControl, BubbleDragState> _dragStates = new();
    
    private class BubbleDragState
    {
        public bool IsDragging = false;
        public Point DragStartPoint = new();
    }
    
    public static void EnableDrag(UserControl bubble, Action<double, double>? onPositionChanged = null, Action? onSave = null)
    {
        if (!_dragStates.ContainsKey(bubble))
        {
            _dragStates[bubble] = new BubbleDragState();
        }
        var state = _dragStates[bubble];
        
        bubble.MouseLeftButtonDown += (sender, e) =>
        {
            var canvas = bubble.Parent as Canvas;
            if (canvas != null)
            {
                state.DragStartPoint = e.GetPosition(canvas);
            }
            state.IsDragging = false;
            bubble.CaptureMouse();
        };

        bubble.MouseMove += (sender, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && bubble.IsMouseCaptured)
            {
                var canvas = bubble.Parent as Canvas;
                if (canvas != null)
                {
                    var currentPosition = e.GetPosition(canvas);
                    
                    if (!state.IsDragging)
                    {
                        var delta = currentPosition - state.DragStartPoint;
                        if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5)
                        {
                            state.IsDragging = true;
                        }
                    }

                    if (state.IsDragging)
                    {
                        var currentLeft = Canvas.GetLeft(bubble);
                        var currentTop = Canvas.GetTop(bubble);
                        var delta = currentPosition - state.DragStartPoint;
                        var newX = currentLeft + delta.X;
                        var newY = currentTop + delta.Y;
                        
                        Canvas.SetLeft(bubble, newX);
                        Canvas.SetTop(bubble, newY);
                        onPositionChanged?.Invoke(newX, newY);
                        state.DragStartPoint = currentPosition;
                    }
                }
            }
        };

        bubble.MouseLeftButtonUp += (sender, e) =>
        {
            var wasDragging = state.IsDragging;
            bubble.ReleaseMouseCapture();
            if (wasDragging)
            {
                // C'était un drag, sauvegarder
                onSave?.Invoke();
            }
            state.IsDragging = false;
            // Si ce n'était pas un drag, laisser le gestionnaire OnMouseLeftButtonUp de la bulle gérer
        };
        
        // Nettoyer quand la bulle est supprimée
        bubble.Unloaded += (sender, e) =>
        {
            _dragStates.Remove(bubble);
        };
    }

    public static void SaveBubblePosition(string propertyName, double x, double y, double opacity, double size)
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                
                // Utiliser la réflexion pour mettre à jour la propriété
                var prop = typeof(Config).GetProperty($"{propertyName}X");
                prop?.SetValue(config, x);
                
                prop = typeof(Config).GetProperty($"{propertyName}Y");
                prop?.SetValue(config, y);
                
                prop = typeof(Config).GetProperty($"{propertyName}Opacity");
                prop?.SetValue(config, opacity);
                
                prop = typeof(Config).GetProperty($"{propertyName}Size");
                prop?.SetValue(config, size);
                
                var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, updatedJson);
            }
        }
        catch { }
    }
}
