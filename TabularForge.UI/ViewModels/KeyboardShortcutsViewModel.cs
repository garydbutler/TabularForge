using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

/// <summary>
/// ViewModel for the Keyboard Shortcuts customization dialog.
/// </summary>
public partial class KeyboardShortcutsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<KeyboardShortcut> _shortcuts = new();

    [ObservableProperty]
    private ObservableCollection<KeyboardShortcut> _filteredShortcuts = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private KeyboardShortcut? _selectedShortcut;

    [ObservableProperty]
    private string _newGesture = string.Empty;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ObservableCollection<string> Categories { get; } = new()
    {
        "All", "File", "Edit", "DAX", "View", "Model", "Tools"
    };

    /// <summary>
    /// Set to true when the user clicks OK to accept changes.
    /// </summary>
    public bool DialogResult { get; set; }

    public KeyboardShortcutsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadShortcuts();
    }

    private void LoadShortcuts()
    {
        var defaults = _settingsService.GetDefaultShortcuts();
        var customized = _settingsService.Settings.CustomShortcuts;

        // Merge custom overrides onto defaults
        foreach (var defaultShortcut in defaults)
        {
            var custom = customized.FirstOrDefault(c => c.CommandId == defaultShortcut.CommandId);
            if (custom != null)
            {
                defaultShortcut.CustomKeyGesture = custom.CustomKeyGesture;
            }
        }

        Shortcuts = new ObservableCollection<KeyboardShortcut>(defaults);
        _settingsService.DetectConflicts(Shortcuts.ToList());
        ApplyFilter();
    }

    /// <summary>
    /// Assigns the new gesture to the selected shortcut.
    /// </summary>
    [RelayCommand]
    private void AssignGesture()
    {
        if (SelectedShortcut == null || string.IsNullOrWhiteSpace(NewGesture)) return;

        SelectedShortcut.CustomKeyGesture = NewGesture.Trim();
        HasChanges = true;

        _settingsService.DetectConflicts(Shortcuts.ToList());

        if (SelectedShortcut.HasConflict)
        {
            StatusText = $"Conflict: '{NewGesture}' is also used by {SelectedShortcut.ConflictWith}";
        }
        else
        {
            StatusText = $"Assigned '{NewGesture}' to {SelectedShortcut.CommandName}";
        }

        NewGesture = string.Empty;
        ApplyFilter();
    }

    /// <summary>
    /// Resets the selected shortcut to its default.
    /// </summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        if (SelectedShortcut == null) return;

        SelectedShortcut.CustomKeyGesture = string.Empty;
        HasChanges = true;
        _settingsService.DetectConflicts(Shortcuts.ToList());
        StatusText = $"Reset {SelectedShortcut.CommandName} to default: {SelectedShortcut.DefaultKeyGesture}";
        ApplyFilter();
    }

    /// <summary>
    /// Resets all shortcuts to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetAllDefaults()
    {
        foreach (var s in Shortcuts)
            s.CustomKeyGesture = string.Empty;

        HasChanges = true;
        _settingsService.DetectConflicts(Shortcuts.ToList());
        StatusText = "All shortcuts reset to defaults";
        ApplyFilter();
    }

    /// <summary>
    /// Saves and closes.
    /// </summary>
    [RelayCommand]
    private void Accept()
    {
        // Save custom shortcuts to settings
        _settingsService.Settings.CustomShortcuts.Clear();
        foreach (var s in Shortcuts.Where(s => s.IsCustomized))
        {
            _settingsService.Settings.CustomShortcuts.Add(new KeyboardShortcut
            {
                CommandId = s.CommandId,
                CustomKeyGesture = s.CustomKeyGesture
            });
        }
        _settingsService.Save();
        DialogResult = true;
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    partial void OnSelectedShortcutChanged(KeyboardShortcut? value)
    {
        if (value != null)
            NewGesture = value.EffectiveGesture;
    }

    private void ApplyFilter()
    {
        var filtered = Shortcuts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(s =>
                s.CommandName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                s.EffectiveGesture.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedCategory != "All")
        {
            filtered = filtered.Where(s => s.Category == SelectedCategory);
        }

        FilteredShortcuts = new ObservableCollection<KeyboardShortcut>(filtered);
    }
}
