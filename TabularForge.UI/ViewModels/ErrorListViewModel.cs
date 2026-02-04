using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabularForge.DAXParser.Semantics;

namespace TabularForge.UI.ViewModels;

public partial class ErrorListViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ErrorListItem> _items = new();

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    [ObservableProperty]
    private bool _showErrors = true;

    [ObservableProperty]
    private bool _showWarnings = true;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private ErrorListItem? _selectedItem;

    [ObservableProperty]
    private string _summaryText = "0 Errors, 0 Warnings";

    public event EventHandler<ErrorListItem>? NavigateToError;

    public void RaiseNavigateToError(ErrorListItem item)
    {
        NavigateToError?.Invoke(this, item);
    }

    public void UpdateDiagnostics(IEnumerable<DaxDiagnostic> diagnostics)
    {
        Items.Clear();
        int errors = 0, warnings = 0, infos = 0;

        foreach (var d in diagnostics)
        {
            var item = new ErrorListItem
            {
                Severity = d.Severity,
                SeverityIcon = d.Severity switch
                {
                    DaxDiagnosticSeverity.Error => "E",
                    DaxDiagnosticSeverity.Warning => "W",
                    _ => "I"
                },
                Message = d.Message,
                Line = d.Line,
                Column = d.Column,
                Source = d.Source ?? string.Empty,
                StartOffset = d.StartOffset,
                Length = d.Length
            };

            Items.Add(item);

            switch (d.Severity)
            {
                case DaxDiagnosticSeverity.Error: errors++; break;
                case DaxDiagnosticSeverity.Warning: warnings++; break;
                default: infos++; break;
            }
        }

        ErrorCount = errors;
        WarningCount = warnings;
        InfoCount = infos;
        SummaryText = $"{errors} Error{(errors != 1 ? "s" : "")}, {warnings} Warning{(warnings != 1 ? "s" : "")}";
    }

    public void Clear()
    {
        Items.Clear();
        ErrorCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        SummaryText = "0 Errors, 0 Warnings";
    }

    [RelayCommand]
    private void ClearAll()
    {
        Clear();
    }

    [RelayCommand]
    private void GoToError()
    {
        if (SelectedItem != null)
        {
            NavigateToError?.Invoke(this, SelectedItem);
        }
    }

    partial void OnShowErrorsChanged(bool value) => OnPropertyChanged(nameof(FilteredItems));
    partial void OnShowWarningsChanged(bool value) => OnPropertyChanged(nameof(FilteredItems));
    partial void OnShowInfoChanged(bool value) => OnPropertyChanged(nameof(FilteredItems));

    public IEnumerable<ErrorListItem> FilteredItems => Items.Where(i =>
        (ShowErrors && i.Severity == DaxDiagnosticSeverity.Error) ||
        (ShowWarnings && i.Severity == DaxDiagnosticSeverity.Warning) ||
        (ShowInfo && i.Severity == DaxDiagnosticSeverity.Info));
}

public partial class ErrorListItem : ObservableObject
{
    [ObservableProperty]
    private DaxDiagnosticSeverity _severity;

    [ObservableProperty]
    private string _severityIcon = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private int _line;

    [ObservableProperty]
    private int _column;

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private int _startOffset;

    [ObservableProperty]
    private int _length;
}
