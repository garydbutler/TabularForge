namespace TabularForge.Core.Models;

/// <summary>
/// Represents a single property of a TOM object for display in the Properties panel.
/// </summary>
public class TomProperty
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string DisplayName { get; set; } = string.Empty;
    public object? Value { get; set; }
    public Type ValueType { get; set; } = typeof(string);
    public bool IsReadOnly { get; set; }
    public string? Description { get; set; }
    public string[]? EnumValues { get; set; }

    public TomProperty() { }

    public TomProperty(string name, object? value, string category = "General", bool isReadOnly = false)
    {
        Name = name;
        DisplayName = name;
        Value = value;
        Category = category;
        IsReadOnly = isReadOnly;
        ValueType = value?.GetType() ?? typeof(string);
    }
}
