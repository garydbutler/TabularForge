using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabularForge.Core.Models;

// ===========================
//  TRANSLATION MODELS
// ===========================

/// <summary>
/// Represents a single translated string for an object property in a specific culture.
/// </summary>
public partial class TranslationEntry : ObservableObject
{
    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _objectType = string.Empty;

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _property = string.Empty; // "Caption", "Description", "DisplayFolder"

    [ObservableProperty]
    private string _defaultValue = string.Empty;

    /// <summary>
    /// Culture code -> translated value dictionary stored as observable entries.
    /// </summary>
    public ObservableCollection<CultureTranslation> Translations { get; } = new();

    /// <summary>
    /// Path to the TOM object for navigation.
    /// </summary>
    public string ObjectPath { get; set; } = string.Empty;
}

/// <summary>
/// A single culture's translation value.
/// </summary>
public partial class CultureTranslation : ObservableObject
{
    [ObservableProperty]
    private string _cultureCode = string.Empty;

    [ObservableProperty]
    private string _translatedValue = string.Empty;

    [ObservableProperty]
    private bool _isModified;
}

// ===========================
//  PERSPECTIVE MODELS
// ===========================

/// <summary>
/// Represents a perspective membership entry: one object's inclusion in perspectives.
/// </summary>
public partial class PerspectiveMembership : ObservableObject
{
    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _objectType = string.Empty;

    [ObservableProperty]
    private string _tableName = string.Empty;

    /// <summary>
    /// Perspective name -> included (bool) mapping.
    /// </summary>
    public ObservableCollection<PerspectiveInclusion> Inclusions { get; } = new();

    /// <summary>
    /// Path to the TOM object for navigation.
    /// </summary>
    public string ObjectPath { get; set; } = string.Empty;
}

/// <summary>
/// Whether an object is included in a specific perspective.
/// </summary>
public partial class PerspectiveInclusion : ObservableObject
{
    [ObservableProperty]
    private string _perspectiveName = string.Empty;

    [ObservableProperty]
    private bool _isIncluded;
}

// ===========================
//  SETTINGS / SHORTCUTS MODELS
// ===========================

/// <summary>
/// Application settings persisted to JSON.
/// </summary>
public partial class AppSettings : ObservableObject
{
    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _activeLayoutPreset = "Default";

    [ObservableProperty]
    private double _windowLeft;

    [ObservableProperty]
    private double _windowTop;

    [ObservableProperty]
    private double _windowWidth = 1440;

    [ObservableProperty]
    private double _windowHeight = 900;

    [ObservableProperty]
    private bool _isMaximized = true;

    public ObservableCollection<string> RecentFiles { get; set; } = new();

    public ObservableCollection<KeyboardShortcut> CustomShortcuts { get; set; } = new();
}

/// <summary>
/// A keyboard shortcut binding.
/// </summary>
public partial class KeyboardShortcut : ObservableObject
{
    [ObservableProperty]
    private string _commandId = string.Empty;

    [ObservableProperty]
    private string _commandName = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _defaultKeyGesture = string.Empty;

    [ObservableProperty]
    private string _customKeyGesture = string.Empty;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private string _conflictWith = string.Empty;

    /// <summary>
    /// The effective gesture: custom if set, otherwise default.
    /// </summary>
    public string EffectiveGesture =>
        string.IsNullOrEmpty(CustomKeyGesture) ? DefaultKeyGesture : CustomKeyGesture;

    /// <summary>
    /// Whether the shortcut has been customized from its default.
    /// </summary>
    public bool IsCustomized => !string.IsNullOrEmpty(CustomKeyGesture)
        && CustomKeyGesture != DefaultKeyGesture;
}

/// <summary>
/// A named layout preset for AvalonDock.
/// </summary>
public partial class LayoutPreset : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isBuiltIn;

    /// <summary>
    /// Serialized AvalonDock layout XML.
    /// </summary>
    public string LayoutXml { get; set; } = string.Empty;
}
