using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

/// <summary>
/// ViewModel for the Perspective Editor panel.
/// Shows a checkbox matrix of objects x perspectives.
/// </summary>
public partial class PerspectiveEditorViewModel : ObservableObject
{
    private readonly PerspectiveService _perspectiveService;

    [ObservableProperty]
    private TomNode? _modelRoot;

    [ObservableProperty]
    private ObservableCollection<PerspectiveMembership> _memberships = new();

    [ObservableProperty]
    private ObservableCollection<string> _perspectives = new();

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
    private PerspectiveMembership? _selectedMembership;

    [ObservableProperty]
    private string _newPerspectiveName = string.Empty;

    [ObservableProperty]
    private string? _selectedPerspective;

    private List<PerspectiveMembership> _allMemberships = new();

    public event EventHandler<string>? MessageLogged;

    public static ObservableCollection<string> ObjectTypeOptions { get; } = new()
    {
        "All", "Table", "Column", "Measure", "Hierarchy"
    };

    public PerspectiveEditorViewModel(PerspectiveService perspectiveService)
    {
        _perspectiveService = perspectiveService;
    }

    /// <summary>
    /// Loads perspective data from the model.
    /// </summary>
    [RelayCommand]
    private void LoadPerspectives()
    {
        if (ModelRoot == null) return;

        var perspList = _perspectiveService.GetPerspectives(ModelRoot);
        Perspectives = new ObservableCollection<string>(perspList);
        if (perspList.Count > 0)
            SelectedPerspective = perspList[0];

        _allMemberships = _perspectiveService.BuildMembershipGrid(ModelRoot);
        ApplyFilter();

        var tables = _allMemberships.Select(m => m.TableName).Distinct().OrderBy(t => t).ToList();
        TableNames = new ObservableCollection<string>(new[] { "All" }.Concat(tables));

        StatusText = $"{_allMemberships.Count} objects, {perspList.Count} perspectives";
        LogMessage($"Perspective grid loaded: {_allMemberships.Count} objects across {perspList.Count} perspectives");
    }

    /// <summary>
    /// Saves perspective changes back to the model.
    /// </summary>
    [RelayCommand]
    private void SavePerspectives()
    {
        if (ModelRoot == null) return;

        _perspectiveService.SaveMemberships(ModelRoot, _allMemberships);
        HasChanges = false;
        StatusText = "Perspectives saved to model";
        LogMessage("Perspectives saved to model");
    }

    /// <summary>
    /// Adds a new perspective.
    /// </summary>
    [RelayCommand]
    private void AddPerspective()
    {
        if (ModelRoot == null || string.IsNullOrWhiteSpace(NewPerspectiveName)) return;

        var name = NewPerspectiveName.Trim();
        if (Perspectives.Contains(name))
        {
            LogMessage($"Perspective '{name}' already exists");
            return;
        }

        _perspectiveService.AddPerspective(ModelRoot, name);
        Perspectives.Add(name);

        // Add inclusion entry for all memberships
        foreach (var m in _allMemberships)
        {
            m.Inclusions.Add(new PerspectiveInclusion
            {
                PerspectiveName = name,
                IsIncluded = false
            });
        }

        NewPerspectiveName = string.Empty;
        SelectedPerspective = name;
        HasChanges = true;
        StatusText = $"Added perspective: {name}";
        LogMessage($"Added perspective: {name}");
    }

    /// <summary>
    /// Removes the selected perspective.
    /// </summary>
    [RelayCommand]
    private void RemovePerspective()
    {
        if (ModelRoot == null || string.IsNullOrEmpty(SelectedPerspective)) return;

        var name = SelectedPerspective;
        var result = MessageBox.Show($"Delete perspective '{name}'?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _perspectiveService.RemovePerspective(ModelRoot, name);
        Perspectives.Remove(name);

        foreach (var m in _allMemberships)
        {
            var inc = m.Inclusions.FirstOrDefault(i => i.PerspectiveName == name);
            if (inc != null) m.Inclusions.Remove(inc);
        }

        SelectedPerspective = Perspectives.FirstOrDefault();
        HasChanges = true;
        StatusText = $"Removed perspective: {name}";
        LogMessage($"Removed perspective: {name}");
    }

    /// <summary>
    /// Select all objects in the selected perspective.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        if (string.IsNullOrEmpty(SelectedPerspective)) return;
        _perspectiveService.BulkSetPerspective(_allMemberships, SelectedPerspective, true);
        HasChanges = true;
        ApplyFilter();
        LogMessage($"Selected all objects in perspective: {SelectedPerspective}");
    }

    /// <summary>
    /// Deselect all objects in the selected perspective.
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        if (string.IsNullOrEmpty(SelectedPerspective)) return;
        _perspectiveService.BulkSetPerspective(_allMemberships, SelectedPerspective, false);
        HasChanges = true;
        ApplyFilter();
        LogMessage($"Deselected all objects in perspective: {SelectedPerspective}");
    }

    /// <summary>
    /// Select all objects of current filtered type.
    /// </summary>
    [RelayCommand]
    private void SelectAllOfType()
    {
        if (string.IsNullOrEmpty(SelectedPerspective) || SelectedObjectType == "All") return;
        _perspectiveService.BulkSetByType(_allMemberships, SelectedPerspective, SelectedObjectType, true);
        HasChanges = true;
        ApplyFilter();
        LogMessage($"Selected all {SelectedObjectType}s in perspective: {SelectedPerspective}");
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnSelectedObjectTypeChanged(string value) => ApplyFilter();
    partial void OnSelectedTableChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allMemberships.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(m =>
                m.ObjectName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                m.TableName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedObjectType != "All")
        {
            filtered = filtered.Where(m => m.ObjectType == SelectedObjectType);
        }

        if (SelectedTable != "All")
        {
            filtered = filtered.Where(m => m.TableName == SelectedTable);
        }

        Memberships = new ObservableCollection<PerspectiveMembership>(filtered);
    }

    public void LogMessage(string message)
    {
        MessageLogged?.Invoke(this, message);
    }
}
