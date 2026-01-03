using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameOverlay.Windows
{
    public partial class CustomContextMenu : UserControl
    {
        public event EventHandler? MenuClosed;
        private List<MenuItemInfo> _items = new List<MenuItemInfo>();

        public CustomContextMenu()
        {
            InitializeComponent();
        }

        public void AddMenuItem(string header, Action onClick)
        {
            _items.Add(new MenuItemInfo { Header = header, OnClick = onClick });
        }

        public void Show(Point position)
        {
            // Créer les items
            ItemsPanel.Children.Clear();
            foreach (var itemInfo in _items)
            {
                var item = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand
                };

                var textBlock = new TextBlock
                {
                    Text = itemInfo.Header,
                    Foreground = new SolidColorBrush(Color.FromRgb(110, 92, 42)),
                    FontSize = 12
                };

                // Effets de survol désactivés pour meilleure lisibilité
                item.MouseLeftButtonUp += (s, e) =>
                {
                    itemInfo.OnClick?.Invoke();
                    Hide();
                };

                item.Child = textBlock;
                ItemsPanel.Children.Add(item);
            }

            // Positionner et afficher
            Canvas.SetLeft(this, position.X);
            Canvas.SetTop(this, position.Y);
            this.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
            MenuClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal class MenuItemInfo
    {
        public string Header { get; set; } = "";
        public Action? OnClick { get; set; }
    }
}

