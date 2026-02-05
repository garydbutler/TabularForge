using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class ImportWizardViewModel : ObservableObject
{
    private readonly ImportService _importService;
    private readonly ConnectionService _connectionService;

    // === Wizard Step ===

    [ObservableProperty]
    private ImportWizardStep _currentStep = ImportWizardStep.SelectSource;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoNext = true;

    [ObservableProperty]
    private string _nextButtonText = "Next >";

    [ObservableProperty]
    private string _statusText = "Select a data source type";

    // === Connection ===

    [ObservableProperty]
    private ImportConnection _connection = new();

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isConnectionTested;

    // === Source Type Selection ===

    [ObservableProperty]
    private ImportSourceType _selectedSourceType = ImportSourceType.SqlServer;

    // === Table Browser ===

    [ObservableProperty]
    private ObservableCollection<ImportTableInfo> _availableTables = new();

    [ObservableProperty]
    private ImportTableInfo? _selectedTable;

    // === Column Configuration ===

    [ObservableProperty]
    private ObservableCollection<ImportColumnInfo> _columns = new();

    [ObservableProperty]
    private string _tableDisplayName = string.Empty;

    // === Preview ===

    [ObservableProperty]
    private DataView? _previewData;

    [ObservableProperty]
    private string _previewStatus = string.Empty;

    [ObservableProperty]
    private bool _isLoadingPreview;

    // === Summary ===

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _partitionExpression = string.Empty;

    // === Result ===

    [ObservableProperty]
    private bool _importSucceeded;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    // === Model Reference ===

    public TomNode? ModelRoot { get; set; }

    // === Events ===

    public event EventHandler<string>? MessageLogged;
    public event EventHandler? ImportCompleted;

    public void LogMessage(string message) => MessageLogged?.Invoke(this, message);

    // === Available Data Types ===

    public string[] TabularDataTypes { get; } = { "String", "Decimal", "Boolean", "DateTime", "Binary" };
    public Array SourceTypes => Enum.GetValues<ImportSourceType>();

    public ImportWizardViewModel(ImportService importService, ConnectionService connectionService)
    {
        _importService = importService;
        _connectionService = connectionService;
    }

    // === Navigation ===

    [RelayCommand]
    private async Task GoNext()
    {
        switch (CurrentStep)
        {
            case ImportWizardStep.SelectSource:
                Connection.SourceType = SelectedSourceType;
                CurrentStep = ImportWizardStep.ConfigureConnection;
                StatusText = "Configure connection settings";
                break;

            case ImportWizardStep.ConfigureConnection:
                if (!IsConnectionTested)
                {
                    await TestConnection();
                    if (!IsConnectionTested) return;
                }
                await BrowseTables();
                CurrentStep = ImportWizardStep.SelectTables;
                StatusText = "Select tables to import";
                break;

            case ImportWizardStep.SelectTables:
                if (SelectedTable == null)
                {
                    StatusText = "Please select a table";
                    return;
                }
                LoadColumns();
                CurrentStep = ImportWizardStep.ConfigureColumns;
                StatusText = "Configure column mappings";
                break;

            case ImportWizardStep.ConfigureColumns:
                await LoadPreview();
                CurrentStep = ImportWizardStep.Preview;
                StatusText = "Review data preview";
                break;

            case ImportWizardStep.Preview:
                GenerateSummary();
                CurrentStep = ImportWizardStep.Summary;
                NextButtonText = "Import";
                StatusText = "Review import summary";
                break;

            case ImportWizardStep.Summary:
                await DoImport();
                break;
        }

        UpdateNavigation();
    }

    [RelayCommand]
    private void GoBack()
    {
        switch (CurrentStep)
        {
            case ImportWizardStep.ConfigureConnection:
                CurrentStep = ImportWizardStep.SelectSource;
                StatusText = "Select a data source type";
                break;

            case ImportWizardStep.SelectTables:
                CurrentStep = ImportWizardStep.ConfigureConnection;
                StatusText = "Configure connection settings";
                break;

            case ImportWizardStep.ConfigureColumns:
                CurrentStep = ImportWizardStep.SelectTables;
                StatusText = "Select tables to import";
                break;

            case ImportWizardStep.Preview:
                CurrentStep = ImportWizardStep.ConfigureColumns;
                StatusText = "Configure column mappings";
                break;

            case ImportWizardStep.Summary:
                CurrentStep = ImportWizardStep.Preview;
                NextButtonText = "Next >";
                StatusText = "Review data preview";
                break;
        }

        UpdateNavigation();
    }

    private void UpdateNavigation()
    {
        CanGoBack = CurrentStep > ImportWizardStep.SelectSource;
        CanGoNext = true;

        if (CurrentStep == ImportWizardStep.Summary)
            NextButtonText = "Import";
        else
            NextButtonText = "Next >";
    }

    // === Source Type Changed ===

    partial void OnSelectedSourceTypeChanged(ImportSourceType value)
    {
        Connection = new ImportConnection { SourceType = value };
        IsConnectionTested = false;
    }

    // === Connection Test ===

    [RelayCommand]
    private async Task TestConnection()
    {
        IsConnecting = true;
        StatusText = "Testing connection...";

        try
        {
            var success = await _importService.TestConnectionAsync(Connection);
            IsConnectionTested = success;
            Connection.IsConnected = success;
            Connection.ConnectionStatus = success ? "Connected" : "Connection failed";
            StatusText = success ? "Connection successful" : "Connection failed";
            LogMessage($"Import: Connection test {(success ? "succeeded" : "failed")}");
        }
        catch (Exception ex)
        {
            IsConnectionTested = false;
            Connection.ConnectionStatus = $"Error: {ex.Message}";
            StatusText = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    // === File Browser ===

    [RelayCommand]
    private void BrowseFile()
    {
        var filter = Connection.SourceType switch
        {
            ImportSourceType.CsvFile => "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            ImportSourceType.ExcelFile => "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
            _ => "All Files (*.*)|*.*"
        };

        var dlg = new OpenFileDialog
        {
            Title = "Select Data File",
            Filter = filter
        };

        if (dlg.ShowDialog() == true)
        {
            Connection.FilePath = dlg.FileName;
            IsConnectionTested = false;
        }
    }

    // === Table Browsing ===

    private async Task BrowseTables()
    {
        AvailableTables.Clear();
        StatusText = "Loading tables...";

        try
        {
            var tables = await _importService.BrowseTablesAsync(Connection);
            foreach (var t in tables)
                AvailableTables.Add(t);

            StatusText = $"Found {tables.Count} tables/views";
            LogMessage($"Import: Found {tables.Count} tables/views");
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading tables: {ex.Message}";
        }
    }

    // === Column Configuration ===

    private void LoadColumns()
    {
        Columns.Clear();
        if (SelectedTable == null) return;

        TableDisplayName = SelectedTable.TableName;

        foreach (var col in SelectedTable.Columns)
        {
            col.IsSelected = true;
            Columns.Add(col);
        }
    }

    [RelayCommand]
    private void SelectAllColumns()
    {
        foreach (var col in Columns)
            col.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAllColumns()
    {
        foreach (var col in Columns)
            col.IsSelected = false;
    }

    [RelayCommand]
    private void AddBlankColumn()
    {
        var col = new ImportColumnInfo
        {
            ColumnName = $"Column{Columns.Count + 1}",
            SourceDataType = "nvarchar",
            TabularDataType = "String",
            IsSelected = true
        };
        Columns.Add(col);
        SelectedTable?.Columns.Add(col);
    }

    [RelayCommand]
    private void RemoveColumn(ImportColumnInfo? col)
    {
        if (col == null) return;
        Columns.Remove(col);
        SelectedTable?.Columns.Remove(col);
    }

    // === Preview ===

    private async Task LoadPreview()
    {
        if (SelectedTable == null) return;

        IsLoadingPreview = true;
        PreviewStatus = "Loading preview...";

        try
        {
            var result = await _importService.PreviewDataAsync(Connection, SelectedTable);
            PreviewData = result.Data.DefaultView;
            PreviewStatus = result.StatusMessage;
            LogMessage($"Import: Preview loaded ({result.Data.Rows.Count} rows)");
        }
        catch (Exception ex)
        {
            PreviewStatus = $"Preview error: {ex.Message}";
        }
        finally
        {
            IsLoadingPreview = false;
        }
    }

    // === Summary ===

    private void GenerateSummary()
    {
        if (SelectedTable == null) return;

        var selectedCols = Columns.Where(c => c.IsSelected).ToList();
        var tableName = string.IsNullOrEmpty(TableDisplayName) ? SelectedTable.TableName : TableDisplayName;

        PartitionExpression = _importService.GeneratePartitionExpression(Connection, SelectedTable);

        SummaryText = $"""
            Import Summary
            ═══════════════════════════════════
            Source Type:    {Connection.SourceType}
            Table Name:     {tableName}
            Columns:        {selectedCols.Count} of {Columns.Count} selected

            Column Details:
            {string.Join("\n", selectedCols.Select(c => $"  • {c.ColumnName} ({c.TabularDataType})"))}

            Partition Expression:
            ───────────────────────────────────
            {PartitionExpression}
            """;
    }

    // === Import Execution ===

    private async Task DoImport()
    {
        if (SelectedTable == null || ModelRoot == null)
        {
            ResultMessage = "No model loaded or no table selected.";
            return;
        }

        StatusText = "Importing...";

        try
        {
            await Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(TableDisplayName))
                    SelectedTable.DisplayName = TableDisplayName;

                var result = _importService.AddTableToModel(ModelRoot, Connection, SelectedTable);

                if (result.Success)
                {
                    ImportSucceeded = true;
                    ResultMessage = $"Successfully imported '{result.TableName}' with {result.ColumnCount} columns.";
                    StatusText = "Import complete";
                    LogMessage($"Import: Table '{result.TableName}' added ({result.ColumnCount} columns)");
                }
                else
                {
                    ImportSucceeded = false;
                    ResultMessage = $"Import failed: {result.ErrorMessage}";
                    StatusText = "Import failed";
                    LogMessage($"Import failed: {result.ErrorMessage}");
                }
            });

            ImportCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ImportSucceeded = false;
            ResultMessage = $"Import error: {ex.Message}";
            StatusText = "Import error";
            LogMessage($"Import error: {ex.Message}");
        }
    }
}
