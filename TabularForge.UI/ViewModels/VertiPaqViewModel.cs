using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TabularForge.Core.Models;
using TabularForge.Core.Services;

namespace TabularForge.UI.ViewModels;

public partial class VertiPaqViewModel : ObservableObject
{
    private readonly VertiPaqService _vertiPaqService;
    private readonly ConnectionService _connectionService;

    // === Statistics ===

    [ObservableProperty]
    private VertiPaqModelStats? _currentStats;

    [ObservableProperty]
    private ObservableCollection<VertiPaqTableStats> _tableStats = new();

    [ObservableProperty]
    private VertiPaqTableStats? _selectedTable;

    [ObservableProperty]
    private ObservableCollection<VertiPaqColumnStats> _selectedTableColumns = new();

    [ObservableProperty]
    private ObservableCollection<VertiPaqRelationshipStats> _relationshipStats = new();

    [ObservableProperty]
    private ObservableCollection<VertiPaqRecommendation> _recommendations = new();

    [ObservableProperty]
    private ObservableCollection<TreemapItem> _treemapItems = new();

    // === Snapshots ===

    [ObservableProperty]
    private ObservableCollection<VertiPaqSnapshot> _snapshots = new();

    [ObservableProperty]
    private VertiPaqSnapshot? _selectedSnapshot;

    // === View State ===

    [ObservableProperty]
    private string _statusText = "Connect to a server and click Collect to analyze.";

    [ObservableProperty]
    private bool _isCollecting;

    [ObservableProperty]
    private bool _hasStats;

    [ObservableProperty]
    private string _selectedView = "Tables";

    // === Summary ===

    [ObservableProperty]
    private string _modelName = "-";

    [ObservableProperty]
    private string _totalSize = "-";

    [ObservableProperty]
    private string _tableCount = "-";

    [ObservableProperty]
    private string _columnCount = "-";

    [ObservableProperty]
    private string _relationshipCount = "-";

    [ObservableProperty]
    private string _collectedAt = "-";

    public event EventHandler<string>? MessageLogged;

    private CancellationTokenSource? _cts;

    public VertiPaqViewModel(VertiPaqService vertiPaqService, ConnectionService connectionService)
    {
        _vertiPaqService = vertiPaqService;
        _connectionService = connectionService;
    }

    partial void OnSelectedTableChanged(VertiPaqTableStats? value)
    {
        SelectedTableColumns.Clear();
        if (value != null)
        {
            foreach (var col in value.Columns.OrderByDescending(c => c.TotalSize))
                SelectedTableColumns.Add(col);
        }
    }

    // === Collect Statistics ===

    [RelayCommand]
    private async Task CollectStatistics()
    {
        if (!_connectionService.IsConnected)
        {
            StatusText = "Not connected to a server.";
            MessageLogged?.Invoke(this, "VertiPaq: Not connected to server.");
            return;
        }

        IsCollecting = true;
        StatusText = "Collecting VertiPaq statistics via DMV queries...";
        MessageLogged?.Invoke(this, "VertiPaq: Starting statistics collection...");

        _cts = new CancellationTokenSource();

        try
        {
            var stats = await _vertiPaqService.CollectStatisticsAsync(_cts.Token);
            CurrentStats = stats;

            // Update summary
            ModelName = stats.ModelName;
            TotalSize = stats.TotalSizeFormatted;
            TableCount = stats.TableCount.ToString();
            ColumnCount = stats.ColumnCount.ToString();
            RelationshipCount = stats.RelationshipCount.ToString();
            CollectedAt = stats.CollectedAt.ToString("yyyy-MM-dd HH:mm:ss");

            // Update collections
            TableStats.Clear();
            foreach (var t in stats.Tables)
                TableStats.Add(t);

            RelationshipStats.Clear();
            foreach (var r in stats.Relationships)
                RelationshipStats.Add(r);

            // Generate recommendations
            var recs = _vertiPaqService.GenerateRecommendations(stats);
            Recommendations.Clear();
            foreach (var r in recs)
                Recommendations.Add(r);

            // Generate treemap data
            var treemap = _vertiPaqService.GenerateTreemapData(stats);
            TreemapItems.Clear();
            foreach (var item in treemap)
                TreemapItems.Add(item);

            HasStats = true;
            StatusText = $"Collected: {stats.TableCount} tables, {stats.ColumnCount} columns, {stats.TotalSizeFormatted} total";
            MessageLogged?.Invoke(this, $"VertiPaq: Collected stats for {stats.ModelName} ({stats.TotalSizeFormatted})");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Collection cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            MessageLogged?.Invoke(this, $"VertiPaq error: {ex.Message}");
        }
        finally
        {
            IsCollecting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelCollection()
    {
        _cts?.Cancel();
        StatusText = "Cancelling...";
    }

    // === Snapshots ===

    [RelayCommand]
    private void SaveSnapshot()
    {
        if (CurrentStats == null) return;

        var snapshot = new VertiPaqSnapshot
        {
            Name = $"Snapshot {Snapshots.Count + 1} - {DateTime.Now:HH:mm}",
            Timestamp = DateTime.Now,
            Stats = CurrentStats
        };

        Snapshots.Add(snapshot);
        MessageLogged?.Invoke(this, $"Snapshot saved: {snapshot.Name}");
        StatusText = $"Snapshot saved: {snapshot.Name}";
    }

    [RelayCommand]
    private void LoadSnapshot(VertiPaqSnapshot? snapshot)
    {
        if (snapshot == null) return;

        CurrentStats = snapshot.Stats;

        // Refresh display
        TableStats.Clear();
        foreach (var t in snapshot.Stats.Tables)
            TableStats.Add(t);

        RelationshipStats.Clear();
        foreach (var r in snapshot.Stats.Relationships)
            RelationshipStats.Add(r);

        ModelName = snapshot.Stats.ModelName;
        TotalSize = snapshot.Stats.TotalSizeFormatted;
        TableCount = snapshot.Stats.TableCount.ToString();
        ColumnCount = snapshot.Stats.ColumnCount.ToString();
        RelationshipCount = snapshot.Stats.RelationshipCount.ToString();
        CollectedAt = snapshot.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        StatusText = $"Loaded snapshot: {snapshot.Name}";
        HasStats = true;
    }

    [RelayCommand]
    private void DeleteSnapshot(VertiPaqSnapshot? snapshot)
    {
        if (snapshot == null) return;
        Snapshots.Remove(snapshot);
    }

    // === View Switching ===

    [RelayCommand]
    private void SwitchView(string? view)
    {
        if (view != null)
            SelectedView = view;
    }

    // === Export ===

    [RelayCommand]
    private void ExportTableStats()
    {
        if (CurrentStats == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Table Statistics",
            Filter = "CSV Files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"vertipaq_tables_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dlg.ShowDialog() != true) return;

        var csv = _vertiPaqService.ExportToCsv(CurrentStats, "table");
        File.WriteAllText(dlg.FileName, csv);
        MessageLogged?.Invoke(this, $"Table stats exported to {dlg.FileName}");
    }

    [RelayCommand]
    private void ExportColumnStats()
    {
        if (CurrentStats == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Column Statistics",
            Filter = "CSV Files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"vertipaq_columns_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dlg.ShowDialog() != true) return;

        var csv = _vertiPaqService.ExportToCsv(CurrentStats, "column");
        File.WriteAllText(dlg.FileName, csv);
        MessageLogged?.Invoke(this, $"Column stats exported to {dlg.FileName}");
    }
}
