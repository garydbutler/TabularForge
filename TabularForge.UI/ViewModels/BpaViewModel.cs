using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class BpaViewModel : ObservableObject
{
    private readonly BpaService _bpaService;

    // === Analysis Results ===

    [ObservableProperty]
    private ObservableCollection<BpaViolation> _violations = new();

    [ObservableProperty]
    private BpaViolation? _selectedViolation;

    // === Rules ===

    [ObservableProperty]
    private ObservableCollection<BpaRule> _rules = new();

    [ObservableProperty]
    private BpaRule? _selectedRule;

    // === Filter State ===

    [ObservableProperty]
    private bool _showErrors = true;

    [ObservableProperty]
    private bool _showWarnings = true;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private string _filterText = string.Empty;

    // === Scan State ===

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "No scan performed";

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    [ObservableProperty]
    private int _totalViolations;

    [ObservableProperty]
    private bool _scanOnSave;

    // === Model Reference ===

    [ObservableProperty]
    private TomNode? _modelRoot;

    [ObservableProperty]
    private bool _isModelLoaded;

    // === Custom Rule Editor ===

    [ObservableProperty]
    private bool _isRuleEditorOpen;

    [ObservableProperty]
    private string _editRuleName = string.Empty;

    [ObservableProperty]
    private string _editRuleDescription = string.Empty;

    [ObservableProperty]
    private BpaSeverity _editRuleSeverity = BpaSeverity.Warning;

    [ObservableProperty]
    private BpaCategory _editRuleCategory = BpaCategory.Maintenance;

    [ObservableProperty]
    private string _editRuleAppliesTo = string.Empty;

    // === Events ===

    public event EventHandler<string>? MessageLogged;
    public event EventHandler<TomNode>? NavigateToObject;

    public void LogMessage(string message) => MessageLogged?.Invoke(this, message);

    public BpaViewModel(BpaService bpaService)
    {
        _bpaService = bpaService;
        RefreshRules();
    }

    // === Scan Command ===

    [RelayCommand]
    private void RunScan()
    {
        if (ModelRoot == null)
        {
            StatusText = "No model loaded";
            LogMessage("BPA: Cannot scan - no model loaded.");
            return;
        }

        IsScanning = true;
        StatusText = "Scanning...";

        try
        {
            var options = new BpaScanOptions
            {
                ScanOnSave = ScanOnSave,
                MinimumSeverity = BpaSeverity.Info
            };

            var results = _bpaService.Analyze(ModelRoot, options);

            Violations.Clear();
            foreach (var v in results)
                Violations.Add(v);

            UpdateCounts();
            ApplyFilter();

            StatusText = $"Scan complete: {TotalViolations} issues found";
            LogMessage($"BPA scan complete: {ErrorCount} errors, {WarningCount} warnings, {InfoCount} info");
        }
        catch (Exception ex)
        {
            StatusText = $"Scan error: {ex.Message}";
            LogMessage($"BPA scan error: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    // === Fix Command ===

    [RelayCommand]
    private void FixViolation(BpaViolation? violation)
    {
        if (violation == null || !violation.IsAutoFixable) return;

        var success = _bpaService.TryFix(violation);
        if (success)
        {
            violation.IsFixed = true;
            LogMessage($"BPA: Fixed - {violation.RuleName} on '{violation.ObjectName}'");

            // Re-scan to update list
            RunScan();
        }
        else
        {
            LogMessage($"BPA: Could not fix - {violation.RuleName} on '{violation.ObjectName}'");
        }
    }

    [RelayCommand]
    private void FixAllAutoFixable()
    {
        var fixable = Violations.Where(v => v.IsAutoFixable && !v.IsFixed).ToList();
        int fixedCount = 0;

        foreach (var v in fixable)
        {
            if (_bpaService.TryFix(v))
            {
                v.IsFixed = true;
                fixedCount++;
            }
        }

        LogMessage($"BPA: Auto-fixed {fixedCount} of {fixable.Count} fixable violations");
        RunScan();
    }

    // === Navigation ===

    [RelayCommand]
    private void GoToObject(BpaViolation? violation)
    {
        if (violation?.ObjectNode == null) return;
        NavigateToObject?.Invoke(this, violation.ObjectNode);
        LogMessage($"BPA: Navigate to {violation.ObjectType} '{violation.ObjectName}'");
    }

    // === Filter ===

    partial void OnShowErrorsChanged(bool value) => ApplyFilter();
    partial void OnShowWarningsChanged(bool value) => ApplyFilter();
    partial void OnShowInfoChanged(bool value) => ApplyFilter();
    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        // Notify UI to re-filter (the View will use CollectionViewSource or similar)
        OnPropertyChanged(nameof(FilteredViolations));
    }

    public IEnumerable<BpaViolation> FilteredViolations
    {
        get
        {
            return Violations.Where(v =>
            {
                if (v.Severity == BpaSeverity.Error && !ShowErrors) return false;
                if (v.Severity == BpaSeverity.Warning && !ShowWarnings) return false;
                if (v.Severity == BpaSeverity.Info && !ShowInfo) return false;
                if (!string.IsNullOrEmpty(FilterText))
                {
                    return v.ObjectName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                           v.RuleName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                           v.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            });
        }
    }

    private void UpdateCounts()
    {
        ErrorCount = Violations.Count(v => v.Severity == BpaSeverity.Error);
        WarningCount = Violations.Count(v => v.Severity == BpaSeverity.Warning);
        InfoCount = Violations.Count(v => v.Severity == BpaSeverity.Info);
        TotalViolations = Violations.Count;
    }

    // === Rule Management ===

    private void RefreshRules()
    {
        Rules.Clear();
        foreach (var rule in _bpaService.Rules)
            Rules.Add(rule);
    }

    [RelayCommand]
    private void ToggleRule(BpaRule? rule)
    {
        if (rule == null) return;
        rule.IsEnabled = !rule.IsEnabled;
        LogMessage($"BPA: Rule '{rule.Name}' {(rule.IsEnabled ? "enabled" : "disabled")}");
    }

    [RelayCommand]
    private void OpenRuleEditor()
    {
        IsRuleEditorOpen = true;
        EditRuleName = string.Empty;
        EditRuleDescription = string.Empty;
        EditRuleSeverity = BpaSeverity.Warning;
        EditRuleCategory = BpaCategory.Maintenance;
        EditRuleAppliesTo = string.Empty;
    }

    [RelayCommand]
    private void SaveCustomRule()
    {
        if (string.IsNullOrWhiteSpace(EditRuleName)) return;

        var rule = new BpaRule
        {
            Name = EditRuleName,
            Description = EditRuleDescription,
            Severity = EditRuleSeverity,
            Category = EditRuleCategory,
            AppliesTo = EditRuleAppliesTo
        };

        _bpaService.AddCustomRule(rule);
        RefreshRules();
        IsRuleEditorOpen = false;
        LogMessage($"BPA: Added custom rule '{rule.Name}'");
    }

    [RelayCommand]
    private void CancelRuleEditor()
    {
        IsRuleEditorOpen = false;
    }

    [RelayCommand]
    private void RemoveRule(BpaRule? rule)
    {
        if (rule == null) return;
        _bpaService.RemoveRule(rule.Id);
        RefreshRules();
        LogMessage($"BPA: Removed rule '{rule.Name}'");
    }

    // === Import/Export Rules ===

    [RelayCommand]
    private void ExportRules()
    {
        var json = _bpaService.ExportRules();
        LogMessage($"BPA: Exported {Rules.Count} rules");
        // In a real implementation, save to file via dialog
    }

    [RelayCommand]
    private void ImportRules()
    {
        // In a real implementation, load from file via dialog
        LogMessage("BPA: Import rules from JSON file");
    }

    // === Severity Arrays for Binding ===

    public Array SeverityValues => Enum.GetValues<BpaSeverity>();
    public Array CategoryValues => Enum.GetValues<BpaCategory>();
}
