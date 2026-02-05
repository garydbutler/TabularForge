using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

public class BpaService
{
    private readonly BimFileService _bimFileService;
    private List<BpaRule> _rules = new();

    public IReadOnlyList<BpaRule> Rules => _rules.AsReadOnly();

    public BpaService(BimFileService bimFileService)
    {
        _bimFileService = bimFileService;
        InitializeBuiltInRules();
    }

    /// <summary>
    /// Run all enabled rules against the model.
    /// </summary>
    public List<BpaViolation> Analyze(TomNode? modelRoot, BpaScanOptions? options = null)
    {
        var violations = new List<BpaViolation>();
        if (modelRoot == null) return violations;

        options ??= new BpaScanOptions();

        var enabledRules = _rules
            .Where(r => r.IsEnabled)
            .Where(r => options.EnabledCategories.Contains(r.Category))
            .Where(r => r.Severity >= options.MinimumSeverity)
            .ToList();

        // Collect all nodes to analyze
        var nodes = new List<TomNode>();
        CollectNodes(modelRoot, nodes, options.IncludeHiddenObjects);

        foreach (var rule in enabledRules)
        {
            var applicableNodes = FilterNodesByType(nodes, rule.AppliesTo);

            foreach (var node in applicableNodes)
            {
                if (EvaluateRule(rule, node, modelRoot))
                {
                    violations.Add(new BpaViolation
                    {
                        Rule = rule,
                        ObjectNode = node,
                        ObjectName = node.Name,
                        ObjectPath = node.GetPath(),
                        ObjectType = node.ObjectType.ToString(),
                        Message = FormatMessage(rule, node)
                    });
                }
            }
        }

        return violations.OrderBy(v => v.Severity).ThenBy(v => v.ObjectPath).ToList();
    }

    /// <summary>
    /// Try to auto-fix a violation.
    /// </summary>
    public bool TryFix(BpaViolation violation)
    {
        if (!violation.IsAutoFixable || violation.ObjectNode == null)
            return false;

        try
        {
            return ApplyFix(violation.Rule, violation.ObjectNode);
        }
        catch
        {
            return false;
        }
    }

    private void CollectNodes(TomNode node, List<TomNode> nodes, bool includeHidden)
    {
        if (!includeHidden && node.IsHidden) return;

        nodes.Add(node);
        foreach (var child in node.Children)
            CollectNodes(child, nodes, includeHidden);
    }

    private List<TomNode> FilterNodesByType(List<TomNode> nodes, string appliesTo)
    {
        if (string.IsNullOrEmpty(appliesTo))
            return nodes;

        var types = appliesTo.Split(',').Select(t => t.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return nodes.Where(n =>
            types.Contains(n.ObjectType.ToString()) ||
            (types.Contains("Column") && (n.ObjectType is TomObjectType.DataColumn
                or TomObjectType.CalculatedColumn or TomObjectType.CalculatedTableColumn)))
            .ToList();
    }

    private bool EvaluateRule(BpaRule rule, TomNode node, TomNode modelRoot)
    {
        // Built-in rule evaluation by ID
        return rule.Id switch
        {
            "NAMING_MEASURE_CAMEL" => EvalNamingMeasureCamel(node),
            "NAMING_COLUMN_PASCAL" => EvalNamingColumnPascal(node),
            "NAMING_TABLE_PASCAL" => EvalNamingTablePascal(node),
            "NAMING_NO_SPACES_MEASURES" => EvalNamingNoSpacesMeasures(node),
            "HIDDEN_COLUMNS_REF" => EvalHiddenColumnsReferenced(node, modelRoot),
            "UNUSED_MEASURE" => EvalUnusedMeasure(node, modelRoot),
            "UNUSED_COLUMN" => EvalUnusedColumn(node, modelRoot),
            "FORMAT_NO_EXPRESSION" => EvalNoExpression(node),
            "FORMAT_DESCRIPTION_EMPTY" => EvalNoDescription(node),
            "PERF_BIDI_RELATIONSHIP" => EvalBidiRelationship(node),
            "PERF_HIGH_CARDINALITY" => EvalHighCardinality(node),
            "DAX_DEPRECATED_FUNCTION" => EvalDeprecatedDaxFunction(node),
            "DAX_USE_REMOVEFILTERS" => EvalUseRemoveFilters(node),
            "DAX_USE_DIVIDE" => EvalUseDivide(node),
            "DAX_AVOID_IFERROR" => EvalAvoidIfError(node),
            _ => EvalCustomExpression(rule, node, modelRoot)
        };
    }

    // === Built-in Rule Evaluations ===

    private bool EvalNamingMeasureCamel(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        if (string.IsNullOrEmpty(node.Name)) return false;
        // Flag if first char is lowercase (should be title case for measures)
        return char.IsLower(node.Name[0]);
    }

    private bool EvalNamingColumnPascal(TomNode node)
    {
        if (node.ObjectType is not (TomObjectType.DataColumn or TomObjectType.CalculatedColumn
            or TomObjectType.CalculatedTableColumn)) return false;
        if (string.IsNullOrEmpty(node.Name)) return false;
        return char.IsLower(node.Name[0]);
    }

    private bool EvalNamingTablePascal(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Table) return false;
        if (string.IsNullOrEmpty(node.Name)) return false;
        return char.IsLower(node.Name[0]);
    }

    private bool EvalNamingNoSpacesMeasures(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        return node.Name.StartsWith(' ') || node.Name.EndsWith(' ');
    }

    private bool EvalHiddenColumnsReferenced(TomNode node, TomNode modelRoot)
    {
        // Check if a hidden column is NOT referenced by any measure
        if (!node.IsHidden) return false;
        if (node.ObjectType is not (TomObjectType.DataColumn or TomObjectType.CalculatedColumn))
            return false;

        // Hidden columns should ideally be referenced by some measure
        var columnRef = $"[{node.Name}]";
        return !IsReferencedInMeasures(columnRef, modelRoot);
    }

    private bool EvalUnusedMeasure(TomNode node, TomNode modelRoot)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        var measureRef = $"[{node.Name}]";
        return !IsReferencedInMeasures(measureRef, modelRoot, excludeSelf: node.Name);
    }

    private bool EvalUnusedColumn(TomNode node, TomNode modelRoot)
    {
        if (node.ObjectType is not (TomObjectType.DataColumn or TomObjectType.CalculatedColumn))
            return false;
        if (node.IsHidden) return false; // Hidden columns handled by other rule

        var columnRef = $"[{node.Name}]";
        return !IsReferencedInMeasures(columnRef, modelRoot) &&
               !IsReferencedInRelationships(node.Name, node.Parent?.Name ?? "", modelRoot);
    }

    private bool EvalNoExpression(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        return string.IsNullOrWhiteSpace(node.Expression);
    }

    private bool EvalNoDescription(TomNode node)
    {
        if (node.ObjectType is not (TomObjectType.Measure or TomObjectType.Table))
            return false;
        return string.IsNullOrWhiteSpace(node.Description);
    }

    private bool EvalBidiRelationship(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Relationship) return false;
        var cfb = node.JsonObject?["crossFilteringBehavior"]?.ToString();
        return cfb?.Equals("bothDirections", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private bool EvalHighCardinality(TomNode node)
    {
        // This is a heuristic - flag columns with "Key" or "ID" in name that are visible
        if (node.ObjectType is not (TomObjectType.DataColumn or TomObjectType.CalculatedColumn))
            return false;
        if (node.IsHidden) return false;

        var name = node.Name.ToUpperInvariant();
        return (name.EndsWith("KEY") || name.EndsWith("ID") || name.EndsWith("SK")) && !node.IsHidden;
    }

    private bool EvalDeprecatedDaxFunction(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        if (string.IsNullOrEmpty(node.Expression)) return false;

        var expr = node.Expression.ToUpperInvariant();
        var deprecated = new[] { "FIRSTNONBLANK", "LASTNONBLANK", "LOOKUPVALUE" };
        return deprecated.Any(f => expr.Contains(f));
    }

    private bool EvalUseRemoveFilters(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        if (string.IsNullOrEmpty(node.Expression)) return false;

        // Flag CALCULATE with ALL used as filter remover (should use REMOVEFILTERS)
        var expr = node.Expression.ToUpperInvariant();
        return expr.Contains("CALCULATE") && Regex.IsMatch(expr, @"\bALL\s*\(");
    }

    private bool EvalUseDivide(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        if (string.IsNullOrEmpty(node.Expression)) return false;

        // Flag direct division operator usage (should use DIVIDE function)
        return node.Expression.Contains('/') && !node.Expression.ToUpperInvariant().Contains("DIVIDE");
    }

    private bool EvalAvoidIfError(TomNode node)
    {
        if (node.ObjectType != TomObjectType.Measure) return false;
        if (string.IsNullOrEmpty(node.Expression)) return false;

        return node.Expression.ToUpperInvariant().Contains("IFERROR");
    }

    private bool EvalCustomExpression(BpaRule rule, TomNode node, TomNode modelRoot)
    {
        // Simple property-based checks from expression string
        if (string.IsNullOrEmpty(rule.Expression)) return false;

        try
        {
            // Simple expression evaluator for common patterns
            return rule.Expression switch
            {
                var e when e.Contains("Name.StartsWith") =>
                    EvalNamePattern(node, rule.Expression),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private bool EvalNamePattern(TomNode node, string expression)
    {
        // Basic pattern: "Name.StartsWith(\"prefix\")"
        var match = Regex.Match(expression, @"Name\.StartsWith\(""([^""]+)""\)");
        if (match.Success)
            return node.Name.StartsWith(match.Groups[1].Value);
        return false;
    }

    // === Helper Methods ===

    private bool IsReferencedInMeasures(string reference, TomNode root, string? excludeSelf = null)
    {
        var measures = new List<TomNode>();
        CollectMeasures(root, measures);

        return measures.Any(m =>
            m.Name != excludeSelf &&
            !string.IsNullOrEmpty(m.Expression) &&
            m.Expression.Contains(reference, StringComparison.OrdinalIgnoreCase));
    }

    private void CollectMeasures(TomNode node, List<TomNode> measures)
    {
        if (node.ObjectType == TomObjectType.Measure)
            measures.Add(node);
        foreach (var child in node.Children)
            CollectMeasures(child, measures);
    }

    private bool IsReferencedInRelationships(string columnName, string tableName, TomNode root)
    {
        var relationships = new List<TomNode>();
        CollectRelationships(root, relationships);

        return relationships.Any(r =>
        {
            var fromTable = r.JsonObject?["fromTable"]?.ToString();
            var fromCol = r.JsonObject?["fromColumn"]?.ToString();
            var toTable = r.JsonObject?["toTable"]?.ToString();
            var toCol = r.JsonObject?["toColumn"]?.ToString();

            return (fromTable == tableName && fromCol == columnName) ||
                   (toTable == tableName && toCol == columnName);
        });
    }

    private void CollectRelationships(TomNode node, List<TomNode> relationships)
    {
        if (node.ObjectType == TomObjectType.Relationship)
            relationships.Add(node);
        foreach (var child in node.Children)
            CollectRelationships(child, relationships);
    }

    private bool ApplyFix(BpaRule rule, TomNode node)
    {
        return rule.Id switch
        {
            "NAMING_MEASURE_CAMEL" => FixCapitalize(node),
            "NAMING_COLUMN_PASCAL" => FixCapitalize(node),
            "NAMING_TABLE_PASCAL" => FixCapitalize(node),
            "NAMING_NO_SPACES_MEASURES" => FixTrimSpaces(node),
            "FORMAT_DESCRIPTION_EMPTY" => FixSetDefaultDescription(node),
            _ => false
        };
    }

    private bool FixCapitalize(TomNode node)
    {
        if (string.IsNullOrEmpty(node.Name)) return false;
        node.Name = char.ToUpper(node.Name[0]) + node.Name[1..];
        return true;
    }

    private bool FixTrimSpaces(TomNode node)
    {
        node.Name = node.Name.Trim();
        return true;
    }

    private bool FixSetDefaultDescription(TomNode node)
    {
        node.Description = $"[Auto] {node.ObjectType}: {node.Name}";
        return true;
    }

    private string FormatMessage(BpaRule rule, TomNode node)
    {
        return $"{rule.Name}: {node.ObjectType} '{node.Name}' - {rule.Description}";
    }

    // === Rule Import/Export ===

    public string ExportRules()
    {
        var ruleSet = new BpaRuleSet
        {
            Name = "TabularForge Rules",
            Description = "Exported BPA rule set",
            Version = "1.0",
            Author = "TabularForge",
            Rules = _rules.Select(r => new BpaRuleDefinition
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Severity = r.Severity.ToString(),
                Category = r.Category.ToString(),
                AppliesTo = r.AppliesTo,
                Expression = r.Expression,
                FixExpression = r.FixExpression,
                FixDescription = r.FixDescription
            }).ToList()
        };

        return JsonConvert.SerializeObject(ruleSet, Formatting.Indented);
    }

    public void ImportRules(string json)
    {
        var ruleSet = JsonConvert.DeserializeObject<BpaRuleSet>(json);
        if (ruleSet?.Rules == null) return;

        foreach (var def in ruleSet.Rules)
        {
            var rule = def.ToRule();
            var existing = _rules.FindIndex(r => r.Id == rule.Id);
            if (existing >= 0)
                _rules[existing] = rule;
            else
                _rules.Add(rule);
        }
    }

    public void AddCustomRule(BpaRule rule)
    {
        if (string.IsNullOrEmpty(rule.Id))
            rule.Id = $"CUSTOM_{Guid.NewGuid():N}";

        _rules.Add(rule);
    }

    public void RemoveRule(string ruleId)
    {
        _rules.RemoveAll(r => r.Id == ruleId);
    }

    // === Built-in Rules Initialization ===

    private void InitializeBuiltInRules()
    {
        _rules = new List<BpaRule>
        {
            // Naming Convention Rules
            new()
            {
                Id = "NAMING_MEASURE_CAMEL",
                Name = "Measure name should start with uppercase",
                Description = "Measure names should use PascalCase or Title Case for readability.",
                Severity = BpaSeverity.Warning,
                Category = BpaCategory.NamingConvention,
                AppliesTo = "Measure",
                FixDescription = "Capitalize first letter"
            },
            new()
            {
                Id = "NAMING_COLUMN_PASCAL",
                Name = "Column name should start with uppercase",
                Description = "Column names should use PascalCase for consistency.",
                Severity = BpaSeverity.Info,
                Category = BpaCategory.NamingConvention,
                AppliesTo = "DataColumn,CalculatedColumn,CalculatedTableColumn",
                FixDescription = "Capitalize first letter"
            },
            new()
            {
                Id = "NAMING_TABLE_PASCAL",
                Name = "Table name should start with uppercase",
                Description = "Table names should use PascalCase.",
                Severity = BpaSeverity.Info,
                Category = BpaCategory.NamingConvention,
                AppliesTo = "Table",
                FixDescription = "Capitalize first letter"
            },
            new()
            {
                Id = "NAMING_NO_SPACES_MEASURES",
                Name = "Measure name has leading/trailing spaces",
                Description = "Measure names should not start or end with spaces.",
                Severity = BpaSeverity.Warning,
                Category = BpaCategory.NamingConvention,
                AppliesTo = "Measure",
                FixDescription = "Trim spaces"
            },

            // Maintenance Rules
            new()
            {
                Id = "HIDDEN_COLUMNS_REF",
                Name = "Hidden column not referenced in measures",
                Description = "Hidden columns should be referenced by at least one measure; otherwise consider removing.",
                Severity = BpaSeverity.Info,
                Category = BpaCategory.Maintenance,
                AppliesTo = "DataColumn,CalculatedColumn"
            },
            new()
            {
                Id = "UNUSED_MEASURE",
                Name = "Potentially unused measure",
                Description = "This measure is not referenced by any other measure. Verify it is used in reports.",
                Severity = BpaSeverity.Info,
                Category = BpaCategory.Maintenance,
                AppliesTo = "Measure"
            },
            new()
            {
                Id = "UNUSED_COLUMN",
                Name = "Potentially unused column",
                Description = "This visible column is not referenced in measures or relationships. Consider hiding it.",
                Severity = BpaSeverity.Info,
                Category = BpaCategory.Maintenance,
                AppliesTo = "DataColumn,CalculatedColumn"
            },

            // Formatting Rules
            new()
            {
                Id = "FORMAT_NO_EXPRESSION",
                Name = "Measure has no expression",
                Description = "Measures should have a DAX expression defined.",
                Severity = BpaSeverity.Error,
                Category = BpaCategory.Formatting,
                AppliesTo = "Measure"
            },
            new()
            {
                Id = "FORMAT_DESCRIPTION_EMPTY",
                Name = "Object has no description",
                Description = "Important objects should have descriptions for documentation.",
                Severity = BpaSeverity.Info,
                Category = BpaCategory.Formatting,
                AppliesTo = "Measure,Table",
                FixDescription = "Set default description"
            },

            // Performance Rules
            new()
            {
                Id = "PERF_BIDI_RELATIONSHIP",
                Name = "Bi-directional relationship detected",
                Description = "Bi-directional relationships can cause unexpected filtering and performance issues.",
                Severity = BpaSeverity.Warning,
                Category = BpaCategory.Performance,
                AppliesTo = "Relationship"
            },
            new()
            {
                Id = "PERF_HIGH_CARDINALITY",
                Name = "High-cardinality column is visible",
                Description = "Key/ID columns with high cardinality should be hidden to avoid accidental use in reports.",
                Severity = BpaSeverity.Warning,
                Category = BpaCategory.Performance,
                AppliesTo = "DataColumn,CalculatedColumn"
            },

            // DAX Best Practice Rules
            new()
            {
                Id = "DAX_DEPRECATED_FUNCTION",
                Name = "Deprecated DAX function used",
                Description = "This measure uses a deprecated or discouraged DAX function.",
                Severity = BpaSeverity.Warning,
                Category = BpaCategory.DAXBestPractice,
                AppliesTo = "Measure"
            },
            new()
            {
                Id = "DAX_USE_REMOVEFILTERS",
                Name = "Use REMOVEFILTERS instead of ALL in CALCULATE",
                Description = "When removing filters in CALCULATE, prefer REMOVEFILTERS over ALL for clarity.",
                Severity = BpaSeverity.Info,
                Category = BpaCategory.DAXBestPractice,
                AppliesTo = "Measure"
            },
            new()
            {
                Id = "DAX_USE_DIVIDE",
                Name = "Use DIVIDE function instead of / operator",
                Description = "DIVIDE handles division by zero gracefully. Prefer DIVIDE over the / operator.",
                Severity = BpaSeverity.Warning,
                Category = BpaCategory.DAXBestPractice,
                AppliesTo = "Measure"
            },
            new()
            {
                Id = "DAX_AVOID_IFERROR",
                Name = "Avoid IFERROR function",
                Description = "IFERROR can mask errors and cause performance issues. Use specific error handling.",
                Severity = BpaSeverity.Warning,
                Category = BpaCategory.DAXBestPractice,
                AppliesTo = "Measure"
            }
        };
    }
}
