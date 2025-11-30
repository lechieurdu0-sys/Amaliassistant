using System;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GameOverlay.Kikimeter.Models;
using GameOverlay.Themes;
using MediaColor = System.Windows.Media.Color;

namespace GameOverlay.Kikimeter.Views;

public partial class XpProgressWindow : Window
{
    public XpProgressWindow(XpProgressViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        UpdateThemeResources();
        ThemeManager.AccentColorChanged += ThemeManagerOnAccentColorChanged;
        ThemeManager.BubbleBackgroundColorChanged += ThemeManagerOnBubbleBackgroundColorChanged;

        Loaded += (_, _) =>
        {
            if (ContextMenu is System.Windows.Controls.ContextMenu menu)
            {
                ThemeManager.ApplyContextMenuTheme(menu);
            }
        };
    }

    public XpProgressViewModel ViewModel { get; }

    public event EventHandler? HideRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler<string>? ColorChanged;
    public event EventHandler? ResetAllRequested;
    private HwndSource? _hwndSource;

    private void HideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResetColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetColorToTheme();
        ColorChanged?.Invoke(this, ViewModel.ProgressColorHex);
    }

    private void ResetAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResetAllRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var dialog = new ColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true
            };

            var drawingColor = ColorTranslator.FromHtml(ViewModel.ProgressColorHex);
            dialog.Color = drawingColor;

            var ownerHandle = new WindowInteropHelper(this).Handle;
            var owner = ownerHandle != IntPtr.Zero ? new Win32Window(ownerHandle) : null;

            System.Windows.Forms.DialogResult result;
            if (owner != null)
            {
                result = dialog.ShowDialog(owner);
            }
            else
            {
                result = dialog.ShowDialog();
            }

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                var color = MediaColor.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                var hex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                ColorChanged?.Invoke(this, hex);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("XpProgressWindow", $"Impossible d'ouvrir le sélecteur de couleur: {ex.Message}");
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (Exception ex)
        {
            Logger.Warning("XpProgressWindow", $"Impossible de déplacer la fenêtre: {ex.Message}");
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        var handle = helper.Handle;
        if (handle != IntPtr.Zero)
        {
            ExcludeFromAltTab(handle);
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource?.AddHook(WindowProc);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        ThemeManager.AccentColorChanged -= ThemeManagerOnAccentColorChanged;
        ThemeManager.BubbleBackgroundColorChanged -= ThemeManagerOnBubbleBackgroundColorChanged;

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WindowProc);
            _hwndSource = null;
        }
    }

    private sealed class Win32Window : System.Windows.Forms.IWin32Window
    {
        public Win32Window(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }

    private void UpdateThemeResources()
    {
        try
        {
            var accentColor = ThemeManager.AccentColor;
            var accentBrush = new SolidColorBrush(accentColor);
            accentBrush.Freeze();
            Resources["CyanAccentBrush"] = accentBrush;

            var bubbleBrush = ThemeManager.BubbleBackgroundBrush;
            if (bubbleBrush is SolidColorBrush bubble)
            {
                var sectionColor = bubble.Color;
                var sectionBrush = new SolidColorBrush(MediaColor.FromArgb((byte)(sectionColor.A * 0.85), sectionColor.R, sectionColor.G, sectionColor.B));
                sectionBrush.Freeze();
                Resources["SectionBackgroundBrush"] = sectionBrush;
            }

            var xpBarColor = MediaColor.FromArgb(90, accentColor.R, accentColor.G, accentColor.B);
            var xpBarBrush = new SolidColorBrush(xpBarColor);
            xpBarBrush.Freeze();
            Resources["XpBarBackgroundBrush"] = xpBarBrush;

            if (ContextMenu is System.Windows.Controls.ContextMenu menu)
            {
                ThemeManager.ApplyContextMenuTheme(menu);
            }
        }
        catch
        {
            // Ignorer pour ne pas perturber l'utilisateur
        }
    }

    private void ThemeManagerOnAccentColorChanged(object? sender, AccentColorChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateThemeResources);
    }

    private void ThemeManagerOnBubbleBackgroundColorChanged(object? sender, BubbleBackgroundColorChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateThemeResources);
    }

    private void ExcludeFromAltTab(IntPtr hwnd)
    {
        try
        {
            uint extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            extendedStyle |= WS_EX_TOOLWINDOW;
            extendedStyle &= ~WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
        }
        catch
        {
            // Ignorer : mieux vaut avoir une fenêtre visible dans Alt+Tab que planter.
        }
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Laisser passer tous les messages
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_APPWINDOW = 0x00040000;
}


