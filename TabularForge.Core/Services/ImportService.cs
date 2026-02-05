using System.Data;
using Newtonsoft.Json.Linq;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

public class ImportService
{
    private readonly BimFileService _bimFileService;

    public ImportService(BimFileService bimFileService)
    {
        _bimFileService = bimFileService;
    }

    /// <summary>
    /// Test connection for the given import configuration.
    /// </summary>
    public async Task<bool> TestConnectionAsync(ImportConnection connection)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (connection.SourceType)
                {
                    case ImportSourceType.SqlServer:
                    case ImportSourceType.AzureSql:
                        // Simulate connection test (actual ADO.NET connection would go here)
                        return !string.IsNullOrEmpty(connection.Server) &&
                               !string.IsNullOrEmpty(connection.Database);

                    case ImportSourceType.CsvFile:
                    case ImportSourceType.ExcelFile:
                        return System.IO.File.Exists(connection.FilePath);

                    case ImportSourceType.OData:
                        return Uri.TryCreate(connection.OdataUrl, UriKind.Absolute, out _);

                    case ImportSourceType.BlankTable:
                        return true;

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Browse available tables/views for the connection.
    /// </summary>
    public async Task<List<ImportTableInfo>> BrowseTablesAsync(ImportConnection connection)
    {
        return await Task.Run(() =>
        {
            var tables = new List<ImportTableInfo>();

            switch (connection.SourceType)
            {
                case ImportSourceType.SqlServer:
                case ImportSourceType.AzureSql:
                    // Generate sample tables for demo (real impl would query INFORMATION_SCHEMA)
                    tables.AddRange(GenerateSampleSqlTables());
                    break;

                case ImportSourceType.CsvFile:
                    if (System.IO.File.Exists(connection.FilePath))
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(connection.FilePath);
                        var columns = ParseCsvHeaders(connection.FilePath);
                        tables.Add(new ImportTableInfo
                        {
                            SchemaName = "",
                            TableName = fileName,
                            TableType = "FILE",
                            Columns = columns
                        });
                    }
                    break;

                case ImportSourceType.ExcelFile:
                    if (System.IO.File.Exists(connection.FilePath))
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(connection.FilePath);
                        tables.Add(new ImportTableInfo
                        {
                            SchemaName = "",
                            TableName = fileName,
                            TableType = "SHEET",
                            Columns = new List<ImportColumnInfo>
                            {
                                new() { ColumnName = "Column1", SourceDataType = "nvarchar", TabularDataType = "String" },
                                new() { ColumnName = "Column2", SourceDataType = "nvarchar", TabularDataType = "String" }
                            }
                        });
                    }
                    break;

                case ImportSourceType.OData:
                    tables.Add(new ImportTableInfo
                    {
                        SchemaName = "",
                        TableName = "ODataEntity",
                        TableType = "ENTITY",
                        Columns = new List<ImportColumnInfo>
                        {
                            new() { ColumnName = "Id", SourceDataType = "int", TabularDataType = "Decimal" },
                            new() { ColumnName = "Name", SourceDataType = "nvarchar", TabularDataType = "String" }
                        }
                    });
                    break;

                case ImportSourceType.BlankTable:
                    tables.Add(new ImportTableInfo
                    {
                        SchemaName = "",
                        TableName = "NewTable",
                        TableType = "BLANK",
                        Columns = new List<ImportColumnInfo>
                        {
                            new() { ColumnName = "Column1", SourceDataType = "nvarchar", TabularDataType = "String" }
                        }
                    });
                    break;
            }

            return tables;
        });
    }

    /// <summary>
    /// Preview data from the source.
    /// </summary>
    public async Task<ImportPreviewResult> PreviewDataAsync(
        ImportConnection connection, ImportTableInfo table, int maxRows = 100)
    {
        return await Task.Run(() =>
        {
            var result = new ImportPreviewResult { PreviewRows = maxRows };
            var dt = new DataTable(table.TableName);

            var selectedColumns = table.Columns.Where(c => c.IsSelected).ToList();

            foreach (var col in selectedColumns)
            {
                dt.Columns.Add(col.ColumnName, typeof(string));
            }

            // Generate sample preview data
            switch (connection.SourceType)
            {
                case ImportSourceType.CsvFile:
                    LoadCsvPreview(connection.FilePath, dt, selectedColumns, maxRows);
                    break;

                default:
                    // Generate sample rows for demo
                    var rng = new Random(42);
                    int rowCount = Math.Min(maxRows, 50);
                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = dt.NewRow();
                        foreach (var col in selectedColumns)
                        {
                            row[col.ColumnName] = col.TabularDataType switch
                            {
                                "Decimal" => rng.Next(1, 10000).ToString(),
                                "Boolean" => (rng.Next(2) == 1).ToString(),
                                "DateTime" => DateTime.Now.AddDays(-rng.Next(365)).ToString("yyyy-MM-dd"),
                                _ => $"Sample_{col.ColumnName}_{i + 1}"
                            };
                        }
                        dt.Rows.Add(row);
                    }
                    result.TotalRows = rowCount;
                    break;
            }

            result.Data = dt;
            result.StatusMessage = $"Loaded {dt.Rows.Count} preview rows";
            return result;
        });
    }

    /// <summary>
    /// Generate the M expression or SQL query for the partition.
    /// </summary>
    public string GeneratePartitionExpression(ImportConnection connection, ImportTableInfo table)
    {
        return connection.SourceType switch
        {
            ImportSourceType.SqlServer or ImportSourceType.AzureSql =>
                GenerateSqlQuery(table),
            ImportSourceType.CsvFile =>
                GenerateCsvMExpression(connection, table),
            ImportSourceType.ExcelFile =>
                GenerateExcelMExpression(connection, table),
            ImportSourceType.OData =>
                GenerateODataMExpression(connection, table),
            ImportSourceType.BlankTable =>
                GenerateBlankTableExpression(table),
            _ => ""
        };
    }

    /// <summary>
    /// Add the imported table to the TOM model.
    /// </summary>
    public ImportResult AddTableToModel(
        TomNode modelRoot, ImportConnection connection, ImportTableInfo table)
    {
        try
        {
            var tableName = string.IsNullOrEmpty(table.DisplayName) ? table.TableName : table.DisplayName;
            var selectedColumns = table.Columns.Where(c => c.IsSelected).ToList();
            var partitionExpr = GeneratePartitionExpression(connection, table);

            // Create the table JSON object
            var tableJson = new JObject
            {
                ["name"] = tableName,
                ["columns"] = new JArray(
                    selectedColumns.Select(c => new JObject
                    {
                        ["name"] = c.ColumnName,
                        ["dataType"] = c.TabularDataType.ToLowerInvariant(),
                        ["sourceColumn"] = c.ColumnName,
                        ["isHidden"] = false
                    })),
                ["partitions"] = new JArray(new JObject
                {
                    ["name"] = $"Partition_{tableName}",
                    ["source"] = new JObject
                    {
                        ["type"] = connection.SourceType switch
                        {
                            ImportSourceType.SqlServer or ImportSourceType.AzureSql => "query",
                            _ => "m"
                        },
                        ["expression"] = partitionExpr
                    }
                })
            };

            // Create TomNode tree for the new table
            var tableNode = new TomNode
            {
                Name = tableName,
                ObjectType = TomObjectType.Table,
                JsonObject = tableJson,
                Parent = FindTablesContainer(modelRoot)
            };

            // Add column nodes
            var columnsFolder = new TomNode
            {
                Name = "Columns",
                ObjectType = TomObjectType.Columns,
                Parent = tableNode
            };
            tableNode.Children.Add(columnsFolder);

            foreach (var col in selectedColumns)
            {
                var colNode = new TomNode
                {
                    Name = col.ColumnName,
                    ObjectType = TomObjectType.DataColumn,
                    DataType = col.TabularDataType,
                    Parent = columnsFolder,
                    JsonObject = (JObject)tableJson["columns"]!
                        .FirstOrDefault(c => c["name"]?.ToString() == col.ColumnName)!
                };
                columnsFolder.Children.Add(colNode);
            }

            // Add partition node
            var partitionsFolder = new TomNode
            {
                Name = "Partitions",
                ObjectType = TomObjectType.Partitions,
                Parent = tableNode
            };
            tableNode.Children.Add(partitionsFolder);

            var partNode = new TomNode
            {
                Name = $"Partition_{tableName}",
                ObjectType = TomObjectType.Partition,
                Expression = partitionExpr,
                Parent = partitionsFolder
            };
            partitionsFolder.Children.Add(partNode);

            // Add to model tree
            var tablesContainer = FindTablesContainer(modelRoot);
            tablesContainer?.Children.Add(tableNode);

            return new ImportResult
            {
                Success = true,
                TableName = tableName,
                PartitionExpression = partitionExpr,
                ColumnCount = selectedColumns.Count
            };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // === Helper Methods ===

    private TomNode? FindTablesContainer(TomNode root)
    {
        if (root.ObjectType == TomObjectType.Tables)
            return root;

        foreach (var child in root.Children)
        {
            var result = FindTablesContainer(child);
            if (result != null) return result;
        }

        return root; // Fallback to root
    }

    private string GenerateSqlQuery(ImportTableInfo table)
    {
        var selectedCols = table.Columns.Where(c => c.IsSelected).Select(c => $"[{c.ColumnName}]");
        return $"SELECT {string.Join(", ", selectedCols)}\nFROM [{table.SchemaName}].[{table.TableName}]";
    }

    private string GenerateCsvMExpression(ImportConnection connection, ImportTableInfo table)
    {
        return $"""
            let
                Source = Csv.Document(File.Contents("{connection.FilePath}"), [Delimiter=",", Encoding=65001, QuoteStyle=QuoteStyle.None]),
                #"Promoted Headers" = Table.PromoteHeaders(Source, [PromoteAllScalars=true])
            in
                #"Promoted Headers"
            """;
    }

    private string GenerateExcelMExpression(ImportConnection connection, ImportTableInfo table)
    {
        return "let\n" +
               $"    Source = Excel.Workbook(File.Contents(\"{connection.FilePath}\"), null, true),\n" +
               $"    Sheet = Source{{[Item=\"{table.TableName}\",Kind=\"Sheet\"]}}[Data],\n" +
               "    #\"Promoted Headers\" = Table.PromoteHeaders(Sheet, [PromoteAllScalars=true])\n" +
               "in\n" +
               "    #\"Promoted Headers\"";
    }

    private string GenerateODataMExpression(ImportConnection connection, ImportTableInfo table)
    {
        return $"""
            let
                Source = OData.Feed("{connection.OdataUrl}", null, [Implementation="2.0"])
            in
                Source
            """;
    }

    private string GenerateBlankTableExpression(ImportTableInfo table)
    {
        var cols = table.Columns.Where(c => c.IsSelected)
            .Select(c => $"{{\"{c.ColumnName}\", type {c.TabularDataType.ToLowerInvariant()}}}");
        var colList = string.Join(", ", cols);
        return "let\n" +
               $"    Source = #table({{{colList}}}, {{}})\n" +
               "in\n" +
               "    Source";
    }

    private List<ImportTableInfo> GenerateSampleSqlTables()
    {
        return new List<ImportTableInfo>
        {
            new()
            {
                SchemaName = "dbo",
                TableName = "Customers",
                TableType = "TABLE",
                Columns = new List<ImportColumnInfo>
                {
                    new() { ColumnName = "CustomerID", SourceDataType = "int", TabularDataType = "Decimal" },
                    new() { ColumnName = "CustomerName", SourceDataType = "nvarchar(200)", TabularDataType = "String" },
                    new() { ColumnName = "Email", SourceDataType = "nvarchar(100)", TabularDataType = "String" },
                    new() { ColumnName = "CreatedDate", SourceDataType = "datetime", TabularDataType = "DateTime" }
                }
            },
            new()
            {
                SchemaName = "dbo",
                TableName = "Orders",
                TableType = "TABLE",
                Columns = new List<ImportColumnInfo>
                {
                    new() { ColumnName = "OrderID", SourceDataType = "int", TabularDataType = "Decimal" },
                    new() { ColumnName = "CustomerID", SourceDataType = "int", TabularDataType = "Decimal" },
                    new() { ColumnName = "OrderDate", SourceDataType = "datetime", TabularDataType = "DateTime" },
                    new() { ColumnName = "TotalAmount", SourceDataType = "decimal(18,2)", TabularDataType = "Decimal" },
                    new() { ColumnName = "Status", SourceDataType = "nvarchar(50)", TabularDataType = "String" }
                }
            },
            new()
            {
                SchemaName = "dbo",
                TableName = "Products",
                TableType = "TABLE",
                Columns = new List<ImportColumnInfo>
                {
                    new() { ColumnName = "ProductID", SourceDataType = "int", TabularDataType = "Decimal" },
                    new() { ColumnName = "ProductName", SourceDataType = "nvarchar(200)", TabularDataType = "String" },
                    new() { ColumnName = "Category", SourceDataType = "nvarchar(100)", TabularDataType = "String" },
                    new() { ColumnName = "Price", SourceDataType = "decimal(18,2)", TabularDataType = "Decimal" },
                    new() { ColumnName = "IsActive", SourceDataType = "bit", TabularDataType = "Boolean" }
                }
            },
            new()
            {
                SchemaName = "dbo",
                TableName = "vw_SalesSummary",
                TableType = "VIEW",
                Columns = new List<ImportColumnInfo>
                {
                    new() { ColumnName = "Year", SourceDataType = "int", TabularDataType = "Decimal" },
                    new() { ColumnName = "Month", SourceDataType = "int", TabularDataType = "Decimal" },
                    new() { ColumnName = "TotalSales", SourceDataType = "decimal(18,2)", TabularDataType = "Decimal" },
                    new() { ColumnName = "OrderCount", SourceDataType = "int", TabularDataType = "Decimal" }
                }
            }
        };
    }

    private List<ImportColumnInfo> ParseCsvHeaders(string filePath)
    {
        var columns = new List<ImportColumnInfo>();
        try
        {
            using var reader = new System.IO.StreamReader(filePath);
            var headerLine = reader.ReadLine();
            if (headerLine != null)
            {
                var headers = headerLine.Split(',');
                foreach (var header in headers)
                {
                    columns.Add(new ImportColumnInfo
                    {
                        ColumnName = header.Trim().Trim('"'),
                        SourceDataType = "nvarchar",
                        TabularDataType = "String"
                    });
                }
            }
        }
        catch { }
        return columns;
    }

    private void LoadCsvPreview(string filePath, DataTable dt, List<ImportColumnInfo> columns, int maxRows)
    {
        try
        {
            using var reader = new System.IO.StreamReader(filePath);
            reader.ReadLine(); // Skip header
            int rowCount = 0;
            while (!reader.EndOfStream && rowCount < maxRows)
            {
                var line = reader.ReadLine();
                if (line == null) break;

                var values = line.Split(',');
                var row = dt.NewRow();
                for (int i = 0; i < Math.Min(values.Length, columns.Count); i++)
                {
                    row[columns[i].ColumnName] = values[i].Trim().Trim('"');
                }
                dt.Rows.Add(row);
                rowCount++;
            }
        }
        catch { }
    }
}
