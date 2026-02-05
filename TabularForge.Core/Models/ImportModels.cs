using CommunityToolkit.Mvvm.ComponentModel;
using System.Data;

namespace TabularForge.Core.Models;

// === Import Source Types ===

public enum ImportSourceType
{
    SqlServer,
    AzureSql,
    CsvFile,
    ExcelFile,
    OData,
    BlankTable
}

// === Import Connection ===

public partial class ImportConnection : ObservableObject
{
    [ObservableProperty]
    private ImportSourceType _sourceType = ImportSourceType.SqlServer;

    [ObservableProperty]
    private string _server = string.Empty;

    [ObservableProperty]
    private string _database = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _useWindowsAuth = true;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _odataUrl = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    public string BuildConnectionString()
    {
        return SourceType switch
        {
            ImportSourceType.SqlServer => BuildSqlConnectionString(),
            ImportSourceType.AzureSql => BuildAzureSqlConnectionString(),
            ImportSourceType.CsvFile => FilePath,
            ImportSourceType.ExcelFile => FilePath,
            ImportSourceType.OData => OdataUrl,
            ImportSourceType.BlankTable => string.Empty,
            _ => string.Empty
        };
    }

    private string BuildSqlConnectionString()
    {
        if (UseWindowsAuth)
            return $"Data Source={Server};Initial Catalog={Database};Integrated Security=true;TrustServerCertificate=true";
        return $"Data Source={Server};Initial Catalog={Database};User Id={Username};Password={Password};TrustServerCertificate=true";
    }

    private string BuildAzureSqlConnectionString()
    {
        return $"Data Source={Server};Initial Catalog={Database};User Id={Username};Password={Password};Encrypt=true;TrustServerCertificate=false";
    }
}

// === Import Table/Column Selection ===

public partial class ImportTableInfo : ObservableObject
{
    [ObservableProperty]
    private string _schemaName = "dbo";

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _tableType = "TABLE"; // TABLE, VIEW

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _displayName = string.Empty;

    public List<ImportColumnInfo> Columns { get; set; } = new();

    public string FullName => string.IsNullOrEmpty(SchemaName) ? TableName : $"{SchemaName}.{TableName}";
}

public partial class ImportColumnInfo : ObservableObject
{
    [ObservableProperty]
    private string _columnName = string.Empty;

    [ObservableProperty]
    private string _sourceDataType = string.Empty;

    [ObservableProperty]
    private string _tabularDataType = "String";

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private bool _isNullable = true;

    [ObservableProperty]
    private int _maxLength;

    public static string MapSqlTypeToTabular(string sqlType)
    {
        var lower = sqlType.ToLowerInvariant();
        if (lower.Contains("int") || lower.Contains("decimal") || lower.Contains("numeric") ||
            lower.Contains("float") || lower.Contains("real") || lower.Contains("money"))
            return "Decimal";
        if (lower.Contains("bit"))
            return "Boolean";
        if (lower.Contains("date") || lower.Contains("time"))
            return "DateTime";
        if (lower.Contains("binary") || lower.Contains("image"))
            return "Binary";
        return "String";
    }
}

// === Import Preview ===

public class ImportPreviewResult
{
    public DataTable Data { get; set; } = new();
    public int TotalRows { get; set; }
    public int PreviewRows { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

// === Generated Partition Expression ===

public class ImportResult
{
    public bool Success { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string PartitionExpression { get; set; } = string.Empty;
    public string DataSourceExpression { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int ColumnCount { get; set; }
}

// === Wizard Step Tracking ===

public enum ImportWizardStep
{
    SelectSource = 0,
    ConfigureConnection = 1,
    SelectTables = 2,
    ConfigureColumns = 3,
    Preview = 4,
    Summary = 5
}
