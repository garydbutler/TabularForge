using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TabularForge.Core.Models;

namespace TabularForge.UI.Views;

public partial class ShortcutReferenceDialog : Window
{
    public ShortcutReferenceDialog(List<KeyboardShortcut> shortcuts)
    {
        InitializeComponent();
        BuildShortcutList(shortcuts);
    }

    private void BuildShortcutList(List<KeyboardShortcut> shortcuts)
    {
        var groups = shortcuts
            .Where(s => !string.IsNullOrEmpty(s.EffectiveGesture))
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            // Category header
            ShortcutList.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 6),
                Foreground = (Brush)FindResource("AccentBrush")
            });

            // Separator
            ShortcutList.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)FindResource("BorderBrush"),
                Margin = new Thickness(0, 0, 0, 6)
            });

            foreach (var shortcut in group.OrderBy(s => s.CommandName))
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.Margin = new Thickness(0, 2, 0, 2);

                var nameBlock = new TextBlock
                {
                    Text = shortcut.CommandName,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource("PrimaryText")
                };
                Grid.SetColumn(nameBlock, 0);

                var gestureBorder = new Border
                {
                    Background = (Brush)FindResource("InputBackground"),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8, 2, 8, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = new TextBlock
                    {
                        Text = shortcut.EffectiveGesture,
                        FontSize = 11,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = (Brush)FindResource("AccentBrush")
                    }
                };
                Grid.SetColumn(gestureBorder, 1);

                row.Children.Add(nameBlock);
                row.Children.Add(gestureBorder);
                ShortcutList.Children.Add(row);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
