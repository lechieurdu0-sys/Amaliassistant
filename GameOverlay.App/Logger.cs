using GameOverlay.Models;

namespace GameOverlay.App;

public static class Logger
{
    public static void Info(string component, string message) => GameOverlay.Models.Logger.Info(component, message);
    public static void Debug(string component, string message) => GameOverlay.Models.Logger.Debug(component, message);
    public static void Error(string component, string message) => GameOverlay.Models.Logger.Error(component, message);
}

