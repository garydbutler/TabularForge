using System.Data;
using System.Diagnostics;
using Microsoft.AnalysisServices.AdomdClient;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

public class QueryService
{
    private readonly ConnectionService _connectionService;

    public QueryService(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string daxQuery, CancellationToken cancellationToken = default)
    {
        if (!_connectionService.IsConnected || _connectionService.CurrentConnection == null)
        {
            return new QueryResult
            {
                ErrorMessage = "Not connected to a server. Please connect first."
            };
        }

        var result = new QueryResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var connStr = BuildAdomdConnectionString();

            await Task.Run(() =>
            {
                using var conn = new AdomdConnection(connStr);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = daxQuery;
                cmd.CommandTimeout = 300; // 5 minute timeout

                using var reader = cmd.ExecuteReader();
                var dataTable = new DataTable();
                dataTable.Load(reader);

                result.Data = dataTable;
                result.RowCount = dataTable.Rows.Count;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Query execution was cancelled.";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            sw.Stop();
            result.ExecutionTime = sw.Elapsed;
        }

        return result;
    }

    public async Task<QueryResult> PreviewTableAsync(string tableName, int topN = 1000, CancellationToken cancellationToken = default)
    {
        var daxQuery = $"EVALUATE TOPN({topN}, '{tableName}')";
        return await ExecuteQueryAsync(daxQuery, cancellationToken);
    }

    public async Task<QueryResult> GetTableRowCountAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var daxQuery = $"EVALUATE ROW(\"RowCount\", COUNTROWS('{tableName}'))";
        return await ExecuteQueryAsync(daxQuery, cancellationToken);
    }

    private string BuildAdomdConnectionString()
    {
        var info = _connectionService.CurrentConnection!;
        var connStr = $"Data Source={info.ServerAddress};";
        if (!string.IsNullOrEmpty(info.DatabaseName))
            connStr += $"Catalog={info.DatabaseName};";
        return connStr;
    }
}
