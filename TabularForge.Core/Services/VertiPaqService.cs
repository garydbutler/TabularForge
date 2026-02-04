using System.Data;
using System.Diagnostics;
using Microsoft.AnalysisServices.AdomdClient;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

public class VertiPaqService
{
    private readonly ConnectionService _connectionService;
    private readonly QueryService _queryService;

    public VertiPaqService(ConnectionService connectionService, QueryService queryService)
    {
        _connectionService = connectionService;
        _queryService = queryService;
    }

    /// <summary>
    /// Collect VertiPaq statistics from the connected model using DMV queries.
    /// </summary>
    public async Task<VertiPaqModelStats> CollectStatisticsAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectionService.IsConnected || _connectionService.CurrentConnection == null)
        {
            return new VertiPaqModelStats { ModelName = "Not connected" };
        }

        var stats = new VertiPaqModelStats
        {
            ModelName = _connectionService.CurrentConnection.DatabaseName,
            CollectedAt = DateTime.Now
        };

        var connStr = BuildAdomdConnectionString();

        await Task.Run(() =>
        {
            using var conn = new AdomdConnection(connStr);
            conn.Open();

            // Collect table stats
            stats.Tables = CollectTableStats(conn);
            stats.Relationships = CollectRelationshipStats(conn);

            // Calculate totals
            stats.TableCount = stats.Tables.Count;
            stats.ColumnCount = stats.Tables.Sum(t => t.ColumnCount);
            stats.RelationshipCount = stats.Relationships.Count;
            stats.TotalSize = stats.Tables.Sum(t => t.TotalSize) + stats.Relationships.Sum(r => r.Size);

            // Calculate percentages
            if (stats.TotalSize > 0)
            {
                foreach (var table in stats.Tables)
                {
                    table.PercentOfModel = (double)table.TotalSize / stats.TotalSize * 100;
                    if (table.TotalSize > 0)
                    {
                        foreach (var col in table.Columns)
                            col.PercentOfTable = (double)col.TotalSize / table.TotalSize * 100;
                    }
                }
            }
        }, cancellationToken);

        return stats;
    }

    private List<VertiPaqTableStats> CollectTableStats(AdomdConnection conn)
    {
        var tables = new Dictionary<string, VertiPaqTableStats>();

        // DMV: DISCOVER_STORAGE_TABLE_COLUMNS for column-level data
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    [TABLE_ID],
                    [DIMENSION_NAME] AS [TableName],
                    [ATTRIBUTE_NAME] AS [ColumnName],
                    [COLUMN_TYPE],
                    [COLUMN_ENCODING],
                    [DICTIONARY_SIZE],
                    [COLUMN_CARDINALITY] AS [Cardinality],
                    [DATATYPE]
                FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMNS
                WHERE LEFT([TABLE_ID], 1) <> '$'
                ORDER BY [DIMENSION_NAME], [ATTRIBUTE_NAME]";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tableName = reader["TableName"]?.ToString() ?? "";
                var colName = reader["ColumnName"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(tableName)) continue;

                if (!tables.ContainsKey(tableName))
                {
                    tables[tableName] = new VertiPaqTableStats { TableName = tableName };
                }

                var table = tables[tableName];
                var col = new VertiPaqColumnStats
                {
                    TableName = tableName,
                    ColumnName = colName,
                    DataType = reader["DATATYPE"]?.ToString() ?? "",
                    Encoding = reader["COLUMN_ENCODING"]?.ToString() ?? "",
                    DictionarySize = SafeLong(reader, "DICTIONARY_SIZE"),
                    Cardinality = SafeLong(reader, "Cardinality"),
                };

                table.Columns.Add(col);
            }
        }
        catch
        {
            // DMV may not be available
        }

        // DMV: DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS for data sizes
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    [DIMENSION_NAME] AS [TableName],
                    [ATTRIBUTE_NAME] AS [ColumnName],
                    [USED_SIZE] AS [DataSize],
                    [TABLE_PARTITION_NAME],
                    [RECORDS_COUNT]
                FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
                WHERE LEFT([TABLE_PARTITION_NAME], 1) <> '$'
                ORDER BY [DIMENSION_NAME]";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tableName = reader["TableName"]?.ToString() ?? "";
                var colName = reader["ColumnName"]?.ToString() ?? "";
                var dataSize = SafeLong(reader, "DataSize");
                var rowCount = SafeLong(reader, "RECORDS_COUNT");

                if (tables.TryGetValue(tableName, out var table))
                {
                    if (rowCount > table.RowCount)
                        table.RowCount = rowCount;

                    var col = table.Columns.FirstOrDefault(c => c.ColumnName == colName);
                    if (col != null)
                    {
                        col.DataSize += dataSize;
                    }
                }
            }
        }
        catch
        {
            // DMV may not be available
        }

        // Calculate table-level totals
        foreach (var table in tables.Values)
        {
            table.ColumnCount = table.Columns.Count;
            foreach (var col in table.Columns)
            {
                col.TotalSize = col.DataSize + col.DictionarySize + col.HierarchySize;
                table.DataSize += col.DataSize;
                table.DictionarySize += col.DictionarySize;
                table.HierarchySize += col.HierarchySize;
            }
            table.TotalSize = table.DataSize + table.DictionarySize + table.HierarchySize;
        }

        return tables.Values.OrderByDescending(t => t.TotalSize).ToList();
    }

    private List<VertiPaqRelationshipStats> CollectRelationshipStats(AdomdConnection conn)
    {
        var relationships = new List<VertiPaqRelationshipStats>();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    [DIMENSION_NAME] AS [FromTable],
                    [ATTRIBUTE_NAME] AS [FromColumn],
                    [RELATED_DIMENSION_NAME] AS [ToTable],
                    [RELATED_ATTRIBUTE_NAME] AS [ToColumn],
                    [USED_SIZE] AS [Size],
                    [RELATIONSHIP_ISACTIVE] AS [IsActive]
                FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
                WHERE [RELATIONSHIP_ISACTIVE] IS NOT NULL
                ORDER BY [USED_SIZE] DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                relationships.Add(new VertiPaqRelationshipStats
                {
                    FromTable = reader["FromTable"]?.ToString() ?? "",
                    FromColumn = reader["FromColumn"]?.ToString() ?? "",
                    ToTable = reader["ToTable"]?.ToString() ?? "",
                    ToColumn = reader["ToColumn"]?.ToString() ?? "",
                    Size = SafeLong(reader, "Size"),
                    IsActive = reader["IsActive"]?.ToString() == "True"
                });
            }
        }
        catch
        {
            // DMV may not be available - try alternative query
        }

        return relationships;
    }

    /// <summary>
    /// Generate optimization recommendations based on statistics.
    /// </summary>
    public List<VertiPaqRecommendation> GenerateRecommendations(VertiPaqModelStats stats)
    {
        var recommendations = new List<VertiPaqRecommendation>();

        foreach (var table in stats.Tables)
        {
            // Large tables (>50% of model)
            if (table.PercentOfModel > 50)
            {
                recommendations.Add(new VertiPaqRecommendation
                {
                    Severity = RecommendationSeverity.Warning,
                    Category = "Table Size",
                    ObjectName = table.TableName,
                    Description = $"Table '{table.TableName}' represents {table.PercentOfModel:F1}% of the model size.",
                    Recommendation = "Consider partitioning, reducing columns, or incremental refresh."
                });
            }

            foreach (var col in table.Columns)
            {
                // High cardinality columns
                if (col.Cardinality > 1_000_000)
                {
                    recommendations.Add(new VertiPaqRecommendation
                    {
                        Severity = RecommendationSeverity.Warning,
                        Category = "High Cardinality",
                        ObjectName = col.FullName,
                        Description = $"Column has {col.Cardinality:N0} unique values.",
                        Recommendation = "Consider reducing cardinality by rounding, grouping, or removing the column."
                    });
                }

                // Large dictionary size relative to data
                if (col.DictionarySize > col.DataSize * 2 && col.DictionarySize > 1_000_000)
                {
                    recommendations.Add(new VertiPaqRecommendation
                    {
                        Severity = RecommendationSeverity.Info,
                        Category = "Dictionary Size",
                        ObjectName = col.FullName,
                        Description = $"Dictionary ({col.DictionarySizeFormatted}) is larger than data ({col.DataSizeFormatted}).",
                        Recommendation = "Consider reducing unique values or changing data type.",
                        PotentialSavings = col.DictionarySize - col.DataSize
                    });
                }

                // Column takes up >30% of the table
                if (col.PercentOfTable > 30 && col.TotalSize > 10_000_000)
                {
                    recommendations.Add(new VertiPaqRecommendation
                    {
                        Severity = RecommendationSeverity.Warning,
                        Category = "Column Size",
                        ObjectName = col.FullName,
                        Description = $"Column represents {col.PercentOfTable:F1}% of table '{table.TableName}'.",
                        Recommendation = "Evaluate if this column is needed in the model."
                    });
                }
            }
        }

        // Check for inactive relationships
        foreach (var rel in stats.Relationships.Where(r => !r.IsActive))
        {
            recommendations.Add(new VertiPaqRecommendation
            {
                Severity = RecommendationSeverity.Info,
                Category = "Inactive Relationship",
                ObjectName = rel.DisplayName,
                Description = "Relationship is inactive and still consumes memory.",
                Recommendation = "Consider removing if not used with USERELATIONSHIP().",
                PotentialSavings = rel.Size
            });
        }

        return recommendations.OrderByDescending(r => r.Severity).ThenByDescending(r => r.PotentialSavings).ToList();
    }

    /// <summary>
    /// Generate treemap data for visualization.
    /// </summary>
    public List<TreemapItem> GenerateTreemapData(VertiPaqModelStats stats)
    {
        var items = new List<TreemapItem>();
        var colors = new[] { "#4A90D9", "#E8854A", "#5CB85C", "#D9534F", "#9B59B6", "#F0AD4E", "#5BC0DE", "#D4A373" };
        int colorIdx = 0;

        foreach (var table in stats.Tables)
        {
            var color = colors[colorIdx % colors.Length];
            colorIdx++;

            items.Add(new TreemapItem
            {
                Name = table.TableName,
                Size = table.TotalSize,
                PercentOfTotal = table.PercentOfModel,
                Color = color
            });
        }

        return items;
    }

    /// <summary>
    /// Export statistics to CSV.
    /// </summary>
    public string ExportToCsv(VertiPaqModelStats stats, string level = "table")
    {
        var sb = new System.Text.StringBuilder();

        if (level == "table")
        {
            sb.AppendLine("Table,Rows,TotalSize,DataSize,DictionarySize,Columns,%OfModel");
            foreach (var t in stats.Tables)
            {
                sb.AppendLine($"\"{t.TableName}\",{t.RowCount},{t.TotalSize},{t.DataSize},{t.DictionarySize},{t.ColumnCount},{t.PercentOfModel:F2}");
            }
        }
        else if (level == "column")
        {
            sb.AppendLine("Table,Column,DataType,Encoding,Cardinality,TotalSize,DataSize,DictionarySize,%OfTable");
            foreach (var t in stats.Tables)
            {
                foreach (var c in t.Columns)
                {
                    sb.AppendLine($"\"{t.TableName}\",\"{c.ColumnName}\",{c.DataType},{c.Encoding},{c.Cardinality},{c.TotalSize},{c.DataSize},{c.DictionarySize},{c.PercentOfTable:F2}");
                }
            }
        }

        return sb.ToString();
    }

    private string BuildAdomdConnectionString()
    {
        var info = _connectionService.CurrentConnection!;
        var connStr = $"Data Source={info.ServerAddress};";
        if (!string.IsNullOrEmpty(info.DatabaseName))
            connStr += $"Catalog={info.DatabaseName};";
        return connStr;
    }

    private static long SafeLong(AdomdDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return 0;
            return Convert.ToInt64(reader[column]);
        }
        catch
        {
            return 0;
        }
    }
}
