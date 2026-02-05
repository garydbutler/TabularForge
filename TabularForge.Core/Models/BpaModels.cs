using CommunityToolkit.Mvvm.ComponentModel;

namespace TabularForge.Core.Models;

// === BPA Rule Definitions ===

public enum BpaSeverity
{
    Error,
    Warning,
    Info
}

public enum BpaCategory
{
    NamingConvention,
    Performance,
    DAXBestPractice,
    Formatting,
    Maintenance,
    Security
}

public partial class BpaRule : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private BpaSeverity _severity = BpaSeverity.Warning;

    [ObservableProperty]
    private BpaCategory _category = BpaCategory.Maintenance;

    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Which TOM object types this rule applies to (e.g. "Measure", "Column", "Table").
    /// Empty = applies to all.
    /// </summary>
    [ObservableProperty]
    private string _appliesTo = string.Empty;

    /// <summary>
    /// C# expression that returns true when the rule is violated.
    /// Available variables: node (TomNode), model (TomNode root).
    /// </summary>
    [ObservableProperty]
    private string _expression = string.Empty;

    /// <summary>
    /// Optional fix action description. If non-empty, the violation is auto-fixable.
    /// </summary>
    [ObservableProperty]
    private string _fixExpression = string.Empty;

    [ObservableProperty]
    private string _fixDescription = string.Empty;

    public bool IsAutoFixable => !string.IsNullOrEmpty(FixExpression);
}

// === BPA Violation ===

public partial class BpaViolation : ObservableObject
{
    [ObservableProperty]
    private BpaRule _rule = new();

    [ObservableProperty]
    private TomNode? _objectNode;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _objectPath = string.Empty;

    [ObservableProperty]
    private string _objectType = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isFixed;

    public BpaSeverity Severity => Rule.Severity;
    public string RuleName => Rule.Name;
    public string CategoryName => Rule.Category.ToString();
    public bool IsAutoFixable => Rule.IsAutoFixable;
}

// === BPA Rule Set ===

public class BpaRuleSet
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Author { get; set; } = string.Empty;
    public List<BpaRuleDefinition> Rules { get; set; } = new();
}

/// <summary>
/// Serializable rule definition for JSON import/export.
/// </summary>
public class BpaRuleDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Category { get; set; } = "Maintenance";
    public string AppliesTo { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string FixExpression { get; set; } = string.Empty;
    public string FixDescription { get; set; } = string.Empty;

    public BpaRule ToRule()
    {
        return new BpaRule
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Severity = Enum.TryParse<BpaSeverity>(Severity, true, out var sev) ? sev : BpaSeverity.Warning,
            Category = Enum.TryParse<BpaCategory>(Category, true, out var cat) ? cat : BpaCategory.Maintenance,
            AppliesTo = AppliesTo,
            Expression = Expression,
            FixExpression = FixExpression,
            FixDescription = FixDescription
        };
    }
}

// === Scan Configuration ===

public class BpaScanOptions
{
    public bool ScanOnSave { get; set; }
    public bool IncludeHiddenObjects { get; set; } = true;
    public List<BpaCategory> EnabledCategories { get; set; } = new(Enum.GetValues<BpaCategory>());
    public BpaSeverity MinimumSeverity { get; set; } = BpaSeverity.Info;
}
