using System.Windows;
using System.Windows.Controls;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.Views;

public partial class LayoutManagerDialog : Window
{
    private readonly SettingsService _settingsService;

    /// <summary>
    /// Fired when user requests to load a layout preset.
    /// </summary>
    public event EventHandler<string>? LayoutLoadRequested;

    /// <summary>
    /// Fired when user requests to save the current layout.
    /// </summary>
    public event EventHandler<string>? LayoutSaveRequested;

    public LayoutManagerDialog(SettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        RefreshPresets();
    }

    private void RefreshPresets()
    {
        var presets = _settingsService.GetLayoutPresets();
        LayoutList.ItemsSource = presets;
    }

    private void LoadLayout_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            LayoutLoadRequested?.Invoke(this, name);
        }
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        var name = NewLayoutName.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        LayoutSaveRequested?.Invoke(this, name);
        NewLayoutName.Text = string.Empty;
        RefreshPresets();
    }

    private void DeleteLayout_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            var result = MessageBox.Show($"Delete layout '{name}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _settingsService.DeleteLayoutPreset(name);
            RefreshPresets();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
