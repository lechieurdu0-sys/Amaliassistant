using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameOverlay.Themes;

namespace GameOverlay.Kikimeter.Views;

public partial class PlayerOrderWindow : Window
{
    public ObservableCollection<PlayerOrderItem> Items { get; }

    private readonly SolidColorBrush _sectionBackgroundBrush;

    public PlayerOrderWindow(IEnumerable<PlayerOrderItem> items, System.Windows.Media.Brush accentBrush, System.Windows.Media.Brush sectionBackgroundBrush)
    {
        InitializeComponent();

        Items = new ObservableCollection<PlayerOrderItem>(items.OrderBy(i => i.Order));
        RenumberItems();

        _sectionBackgroundBrush = sectionBackgroundBrush.CloneCurrentValue() as SolidColorBrush
                                  ?? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00));
        _sectionBackgroundBrush.Freeze();

        ApplyTheme(accentBrush);
        Resources["SectionBackgroundBrush"] = _sectionBackgroundBrush;

        DataContext = this;

        ThemeManager.AccentColorChanged += ThemeManager_AccentColorChanged;
        Closed += PlayerOrderWindow_Closed;
        Loaded += PlayerOrderWindow_Loaded;
    }

    private void PlayerOrderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (PlayerListBox.Items.Count > 0)
        {
            PlayerListBox.SelectedIndex = 0;
            PlayerListBox.Focus();
        }
        UpdateButtonStates();
    }

    private void PlayerOrderWindow_Closed(object? sender, EventArgs e)
    {
        ThemeManager.AccentColorChanged -= ThemeManager_AccentColorChanged;
    }

    private void ThemeManager_AccentColorChanged(object? sender, AccentColorChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateAccentBrush);
    }

    private void ApplyTheme(System.Windows.Media.Brush accentBrush)
    {
        // Les couleurs sont maintenant codées en dur dans le XAML pour correspondre au thème
        // Plus besoin de définir CyanAccentBrush
    }

    private void UpdateAccentBrush()
    {
        // Les couleurs sont maintenant codées en dur dans le XAML pour correspondre au thème
        // Plus besoin de mettre à jour CyanAccentBrush
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(-1);

    private void MoveDownButton_Click(object sender, RoutedEventArgs e) => MoveSelectedItem(1);

    private void MoveSelectedItem(int delta)
    {
        int index = PlayerListBox.SelectedIndex;
        if (index < 0)
            return;

        int newIndex = index + delta;
        if (newIndex < 0 || newIndex >= Items.Count)
            return;

        Items.Move(index, newIndex);
        RenumberItems();
        PlayerListBox.SelectedIndex = newIndex;
        PlayerListBox.ScrollIntoView(PlayerListBox.SelectedItem);
    }

    private void RenumberItems()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].Order = i;
        }
    }

    private void PlayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        int index = PlayerListBox.SelectedIndex;
        MoveUpButton.IsEnabled = index > 0;
        MoveDownButton.IsEnabled = index >= 0 && index < Items.Count - 1;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        // Ne pas utiliser Close() car la fenêtre est gérée par MainWindow avec Hide()
        Hide();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        // Ne pas utiliser Close() car la fenêtre est gérée par MainWindow avec Hide()
        Hide();
    }

    public IReadOnlyList<string> GetOrderedNames() => Items.Select(item => item.Name).ToList();
}

public sealed class PlayerOrderItem : INotifyPropertyChanged
{
    public string Name { get; }

    private int _order;
    public int Order
    {
        get => _order;
        set
        {
            if (_order != value)
            {
                _order = value;
                OnPropertyChanged(nameof(Order));
                OnPropertyChanged(nameof(Display));
            }
        }
    }

    public string Display => $"{Order + 1}. {Name}";

    public PlayerOrderItem(string name, int order)
    {
        Name = name;
        _order = order;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

