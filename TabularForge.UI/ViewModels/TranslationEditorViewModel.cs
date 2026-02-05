using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

/// <summary>
/// ViewModel for the Metadata Translation Editor panel.
/// Shows a grid of objects x cultures for editing translations.
/// </summary>
public partial class TranslationEditorViewModel : ObservableObject
{
    private readonly TranslationService _translationService;

    [ObservableProperty]
    private TomNode? _modelRoot;

    [ObservableProperty]
    private ObservableCollection<TranslationEntry> _entries = new();

    [ObservableProperty]
    private ObservableCollection<string> _cultures = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _selectedObjectType = "All";

    [ObservableProperty]
    private string _selectedTable = "All";

    [ObservableProperty]
    private ObservableCollection<string> _tableNames = new();

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string _statusText = "No model loaded";

    [ObservableProperty]
    private TranslationEntry? _selectedEntry;

    [ObservableProperty]
    private string _newCultureCode = string.Empty;

    private List<TranslationEntry> _allEntries = new();

    public event EventHandler<string>? MessageLogged;

    public static ObservableCollection<string> ObjectTypeOptions { get; } = new()
    {
        "All", "Table", "Column", "Measure", "Hierarchy"
    };

    public TranslationEditorViewModel(TranslationService translationService)
    {
        _translationService = translationService;
    }

    /// <summary>
    /// Loads translation data from the model.
    /// </summary>
    [RelayCommand]
    private void LoadTranslations()
    {
        if (ModelRoot == null) return;

        var cultureList = _translationService.GetCultures(ModelRoot);
        Cultures = new ObservableCollection<string>(cultureList);

        _allEntries = _translationService.BuildTranslationGrid(ModelRoot);
        ApplyFilter();

        // Collect table names for filter
        var tables = _allEntries.Select(e => e.TableName).Distinct().OrderBy(t => t).ToList();
        TableNames = new ObservableCollection<string>(new[] { "All" }.Concat(tables));

        StatusText = $"{_allEntries.Count} entries, {cultureList.Count} cultures";
        LogMessage($"Translation grid loaded: {_allEntries.Count} entries across {cultureList.Count} cultures");
    }

    /// <summary>
    /// Saves translation changes back to the model.
    /// </summary>
    [RelayCommand]
    private void SaveTranslations()
    {
        if (ModelRoot == null) return;

        _translationService.SaveTranslations(ModelRoot, _allEntries);
        HasChanges = false;
        StatusText = "Translations saved to model";
        LogMessage("Translations saved to model");
    }

    /// <summary>
    /// Adds a new culture to the model.
    /// </summary>
    [RelayCommand]
    private void AddCulture()
    {
        if (ModelRoot == null || string.IsNullOrWhiteSpace(NewCultureCode)) return;

        var code = NewCultureCode.Trim();
        if (Cultures.Contains(code))
        {
            LogMessage($"Culture '{code}' already exists");
            return;
        }

        _translationService.AddCulture(ModelRoot, code);
        Cultures.Add(code);

        // Add empty translation for each entry
        foreach (var entry in _allEntries)
        {
            entry.Translations.Add(new CultureTranslation
            {
                CultureCode = code,
                TranslatedValue = string.Empty
            });
        }

        NewCultureCode = string.Empty;
        HasChanges = true;
        StatusText = $"Added culture: {code}";
        LogMessage($"Added culture: {code}");
    }

    /// <summary>
    /// Removes a culture from the model.
    /// </summary>
    [RelayCommand]
    private void RemoveCulture(string cultureCode)
    {
        if (ModelRoot == null || string.IsNullOrEmpty(cultureCode)) return;

        _translationService.RemoveCulture(ModelRoot, cultureCode);
        Cultures.Remove(cultureCode);

        foreach (var entry in _allEntries)
        {
            var trans = entry.Translations.FirstOrDefault(t => t.CultureCode == cultureCode);
            if (trans != null) entry.Translations.Remove(trans);
        }

        HasChanges = true;
        StatusText = $"Removed culture: {cultureCode}";
        LogMessage($"Removed culture: {cultureCode}");
    }

    /// <summary>
    /// Exports translations to CSV file.
    /// </summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Translations (CSV)",
            Filter = "CSV Files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = "translations.csv"
        };

        if (dlg.ShowDialog() != true) return;

        var csv = _translationService.ExportToCsv(_allEntries, Cultures.ToList());
        System.IO.File.WriteAllText(dlg.FileName, csv);
        LogMessage($"Exported translations to: {dlg.FileName}");
    }

    /// <summary>
    /// Exports translations to JSON file.
    /// </summary>
    [RelayCommand]
    private void ExportJson()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Translations (JSON)",
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "translations.json"
        };

        if (dlg.ShowDialog() != true) return;

        var json = _translationService.ExportToJson(_allEntries, Cultures.ToList());
        System.IO.File.WriteAllText(dlg.FileName, json);
        LogMessage($"Exported translations to: {dlg.FileName}");
    }

    /// <summary>
    /// Imports translations from a JSON file.
    /// </summary>
    [RelayCommand]
    private void ImportJson()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Translations (JSON)",
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = System.IO.File.ReadAllText(dlg.FileName);
            _translationService.ImportFromJson(json, _allEntries);
            HasChanges = true;
            ApplyFilter(); // Refresh display
            LogMessage($"Imported translations from: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            LogMessage($"Error importing translations: {ex.Message}");
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnSelectedObjectTypeChanged(string value) => ApplyFilter();
    partial void OnSelectedTableChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allEntries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(e =>
                e.ObjectName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                e.TableName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedObjectType != "All")
        {
            filtered = filtered.Where(e => e.ObjectType == SelectedObjectType);
        }

        if (SelectedTable != "All")
        {
            filtered = filtered.Where(e => e.TableName == SelectedTable);
        }

        Entries = new ObservableCollection<TranslationEntry>(filtered);
    }

    public void LogMessage(string message)
    {
        MessageLogged?.Invoke(this, message);
    }
}
