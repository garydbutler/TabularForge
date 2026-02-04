using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabularForge.Core.Models;

// === VertiPaq Statistics ===

public class VertiPaqModelStats
{
    public string ModelName { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public int TableCount { get; set; }
    public int ColumnCount { get; set; }
    public int RelationshipCount { get; set; }
    public DateTime CollectedAt { get; set; } = DateTime.Now;
    public List<VertiPaqTableStats> Tables { get; set; } = new();
    public List<VertiPaqRelationshipStats> Relationships { get; set; } = new();

    public string TotalSizeFormatted => FormatSize(TotalSize);

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

public class VertiPaqTableStats
{
    public string TableName { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public long TotalSize { get; set; }
    public long DataSize { get; set; }
    public long DictionarySize { get; set; }
    public long HierarchySize { get; set; }
    public int ColumnCount { get; set; }
    public double PercentOfModel { get; set; }
    public List<VertiPaqColumnStats> Columns { get; set; } = new();

    public string TotalSizeFormatted => VertiPaqModelStats.FormatSize(TotalSize);
    public string DataSizeFormatted => VertiPaqModelStats.FormatSize(DataSize);
    public string DictionarySizeFormatted => VertiPaqModelStats.FormatSize(DictionarySize);
}

public class VertiPaqColumnStats
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Encoding { get; set; } = string.Empty;
    public long Cardinality { get; set; }
    public long TotalSize { get; set; }
    public long DataSize { get; set; }
    public long DictionarySize { get; set; }
    public long HierarchySize { get; set; }
    public double PercentOfTable { get; set; }

    public string FullName => $"'{TableName}'[{ColumnName}]";
    public string TotalSizeFormatted => VertiPaqModelStats.FormatSize(TotalSize);
    public string DataSizeFormatted => VertiPaqModelStats.FormatSize(DataSize);
    public string DictionarySizeFormatted => VertiPaqModelStats.FormatSize(DictionarySize);
}

public class VertiPaqRelationshipStats
{
    public string FromTable { get; set; } = string.Empty;
    public string FromColumn { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
    public long Size { get; set; }
    public long FromCardinality { get; set; }
    public long ToCardinality { get; set; }
    public double MissingKeys { get; set; }
    public bool IsActive { get; set; }

    public string DisplayName => $"{FromTable}[{FromColumn}] -> {ToTable}[{ToColumn}]";
    public string SizeFormatted => VertiPaqModelStats.FormatSize(Size);
}

// === Optimization Recommendations ===

public enum RecommendationSeverity
{
    Info,
    Warning,
    Critical
}

public class VertiPaqRecommendation
{
    public RecommendationSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public long PotentialSavings { get; set; }

    public string PotentialSavingsFormatted => PotentialSavings > 0
        ? VertiPaqModelStats.FormatSize(PotentialSavings)
        : string.Empty;
}

// === Treemap Data ===

public class TreemapItem
{
    public string Name { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public long Size { get; set; }
    public double PercentOfTotal { get; set; }
    public string Color { get; set; } = "#4A90D9";
    public string SizeFormatted => VertiPaqModelStats.FormatSize(Size);
}

// === Snapshot Comparison ===

public class VertiPaqSnapshot
{
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public VertiPaqModelStats Stats { get; set; } = new();
}
