using Newtonsoft.Json;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

/// <summary>
/// Service for persisting application settings to a JSON file in user AppData.
/// </summary>
public class SettingsService
{
    private readonly string _settingsDir;
    private readonly string _settingsPath;
    private readonly string _layoutDir;
    private AppSettings _settings;

    public AppSettings Settings => _settings;
    public string LayoutDirectory => _layoutDir;

    public SettingsService()
    {
        _settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabularForge");
        _settingsPath = Path.Combine(_settingsDir, "AppSettings.json");
        _layoutDir = Path.Combine(_settingsDir, "Layouts");
        _settings = new AppSettings();
    }

    /// <summary>
    /// Loads settings from disk. Returns default settings if file doesn't exist.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
        return _settings;
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silently fail on settings save
        }
    }

    /// <summary>
    /// Saves an AvalonDock layout to a named preset file.
    /// </summary>
    public void SaveLayoutPreset(string name, string layoutXml)
    {
        try
        {
            Directory.CreateDirectory(_layoutDir);
            var path = Path.Combine(_layoutDir, $"{SanitizeFileName(name)}.xml");
            File.WriteAllText(path, layoutXml);
        }
        catch
        {
            // Silently fail
        }
    }

    /// <summary>
    /// Loads a named layout preset.
    /// </summary>
    public string? LoadLayoutPreset(string name)
    {
        try
        {
            var path = Path.Combine(_layoutDir, $"{SanitizeFileName(name)}.xml");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        catch
        {
            // Silently fail
        }
        return null;
    }

    /// <summary>
    /// Lists all saved layout presets.
    /// </summary>
    public List<LayoutPreset> GetLayoutPresets()
    {
        var presets = new List<LayoutPreset>
        {
            new LayoutPreset { Name = "Default", Description = "Default layout with all panels", IsBuiltIn = true },
            new LayoutPreset { Name = "DAX Focus", Description = "Maximized editor area, hidden side panels", IsBuiltIn = true },
            new LayoutPreset { Name = "Diagram Focus", Description = "Full diagram view with properties", IsBuiltIn = true }
        };

        try
        {
            if (Directory.Exists(_layoutDir))
            {
                foreach (var file in Directory.GetFiles(_layoutDir, "*.xml"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name is not ("Default" or "DAX Focus" or "Diagram Focus"))
                    {
                        presets.Add(new LayoutPreset
                        {
                            Name = name,
                            Description = "User saved layout",
                            LayoutXml = File.ReadAllText(file)
                        });
                    }
                }
            }
        }
        catch
        {
            // Silently fail
        }

        return presets;
    }

    /// <summary>
    /// Deletes a user layout preset.
    /// </summary>
    public bool DeleteLayoutPreset(string name)
    {
        try
        {
            var path = Path.Combine(_layoutDir, $"{SanitizeFileName(name)}.xml");
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // Silently fail
        }
        return false;
    }

    /// <summary>
    /// Gets all default keyboard shortcuts.
    /// </summary>
    public List<KeyboardShortcut> GetDefaultShortcuts()
    {
        return new List<KeyboardShortcut>
        {
            // File
            new() { CommandId = "NewModel", CommandName = "New Model", Category = "File", DefaultKeyGesture = "Ctrl+N" },
            new() { CommandId = "OpenFile", CommandName = "Open File", Category = "File", DefaultKeyGesture = "Ctrl+O" },
            new() { CommandId = "SaveFile", CommandName = "Save", Category = "File", DefaultKeyGesture = "Ctrl+S" },
            new() { CommandId = "SaveFileAs", CommandName = "Save As", Category = "File", DefaultKeyGesture = "Ctrl+Shift+S" },

            // Edit
            new() { CommandId = "Undo", CommandName = "Undo", Category = "Edit", DefaultKeyGesture = "Ctrl+Z" },
            new() { CommandId = "Redo", CommandName = "Redo", Category = "Edit", DefaultKeyGesture = "Ctrl+Y" },
            new() { CommandId = "Delete", CommandName = "Delete", Category = "Edit", DefaultKeyGesture = "Delete" },
            new() { CommandId = "Rename", CommandName = "Rename", Category = "Edit", DefaultKeyGesture = "F2" },

            // DAX
            new() { CommandId = "ApplyDax", CommandName = "Apply DAX Expression", Category = "DAX", DefaultKeyGesture = "F5" },
            new() { CommandId = "CheckSemantics", CommandName = "Check DAX Semantics", Category = "DAX", DefaultKeyGesture = "Ctrl+F7" },
            new() { CommandId = "OpenDaxScript", CommandName = "Open DAX Script", Category = "DAX", DefaultKeyGesture = "Ctrl+Shift+D" },

            // View
            new() { CommandId = "ToggleTheme", CommandName = "Toggle Theme", Category = "View", DefaultKeyGesture = "Ctrl+Shift+T" },
            new() { CommandId = "ExpandAll", CommandName = "Expand All", Category = "View", DefaultKeyGesture = "Ctrl+Shift+E" },
            new() { CommandId = "CollapseAll", CommandName = "Collapse All", Category = "View", DefaultKeyGesture = "Ctrl+Shift+C" },

            // Model
            new() { CommandId = "ToggleHidden", CommandName = "Toggle Hidden", Category = "Model", DefaultKeyGesture = "Ctrl+I" },
            new() { CommandId = "ConnectToServer", CommandName = "Connect to Server", Category = "Model", DefaultKeyGesture = "Ctrl+Shift+K" },

            // Tools
            new() { CommandId = "OpenDaxQuery", CommandName = "DAX Query Editor", Category = "Tools", DefaultKeyGesture = "Ctrl+Shift+Q" },
            new() { CommandId = "OpenDiagram", CommandName = "Diagram View", Category = "Tools", DefaultKeyGesture = "Ctrl+Shift+G" },
            new() { CommandId = "OpenCSharpScript", CommandName = "C# Script Editor", Category = "Tools", DefaultKeyGesture = "Ctrl+Shift+R" },
            new() { CommandId = "OpenBpa", CommandName = "Best Practice Analyzer", Category = "Tools", DefaultKeyGesture = "Ctrl+Shift+B" },
        };
    }

    /// <summary>
    /// Detects shortcut conflicts in the current configuration.
    /// </summary>
    public void DetectConflicts(List<KeyboardShortcut> shortcuts)
    {
        // Reset all conflicts
        foreach (var s in shortcuts)
        {
            s.HasConflict = false;
            s.ConflictWith = string.Empty;
        }

        // Group by effective gesture
        var groups = shortcuts
            .Where(s => !string.IsNullOrEmpty(s.EffectiveGesture))
            .GroupBy(s => s.EffectiveGesture);

        foreach (var group in groups.Where(g => g.Count() > 1))
        {
            var items = group.ToList();
            foreach (var item in items)
            {
                item.HasConflict = true;
                item.ConflictWith = string.Join(", ",
                    items.Where(i => i != item).Select(i => i.CommandName));
            }
        }
    }

    /// <summary>
    /// Adds a file to the recent files list.
    /// </summary>
    public void AddRecentFile(string filePath)
    {
        _settings.RecentFiles.Remove(filePath);
        _settings.RecentFiles.Insert(0, filePath);
        while (_settings.RecentFiles.Count > 10)
            _settings.RecentFiles.RemoveAt(_settings.RecentFiles.Count - 1);
    }

    private string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }
}
