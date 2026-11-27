using System;
using System.Linq;

namespace GameOverlay.Models;

public static class BrowserPreferences
{
    public static bool IsValidProfile(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 200 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    public static bool IsWebAddress(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrEmpty(uri.Host);

    public static string ResolveAddress(string input)
    {
        string value = input.Trim();
        if (IsWebAddress(value)) return value;
        if (!value.Any(char.IsWhiteSpace) && value.Contains('.') &&
            !value.Contains("://", StringComparison.Ordinal) &&
            IsWebAddress("https://" + value))
            return "https://" + value;
        return "https://www.google.com/search?q=" + Uri.EscapeDataString(value);
    }
}
