using System;
using GameOverlay.Models;

internal static class BrowserPreferencesRegression
{
    public static void Run()
    {
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        Check(BrowserPreferences.ResolveAddress("  wakfu.com  ") == "https://wakfu.com", "Domain navigation");
        Check(BrowserPreferences.ResolveAddress("https://wakfu.com/fr") == "https://wakfu.com/fr", "Full URL navigation");
        Check(BrowserPreferences.ResolveAddress("Wakfu Bellaphone") == "https://www.google.com/search?q=Wakfu%20Bellaphone", "Search navigation");
        Check(!BrowserPreferences.IsWebAddress("file:///C:/test.html"), "Unexpected persistent local file URL");
        Check(BrowserPreferences.IsValidProfile("google_com") && !BrowserPreferences.IsValidProfile("../other") &&
              !BrowserPreferences.IsValidProfile("C:\\other"), "Unsafe browser profile path");
    }
}
