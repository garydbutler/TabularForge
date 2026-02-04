using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

/// <summary>
/// Service for loading and saving .bim (Tabular Model Definition) files.
/// Parses the JSON structure into TomNode tree and serializes back.
/// </summary>
public class BimFileService
{
    private JObject? _rootJson;

    /// <summary>
    /// Load a .bim file and parse it into a TomNode tree.
    /// </summary>
    public TomNode LoadBimFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        _rootJson = JObject.Parse(json);

        var modelJson = _rootJson["model"] as JObject ?? new JObject();
        var modelNode = new TomNode(
            modelJson["name"]?.ToString() ?? Path.GetFileNameWithoutExtension(filePath),
            TomObjectType.Model)
        {
            JsonObject = modelJson,
            Description = modelJson["description"]?.ToString() ?? string.Empty,
            IsExpanded = true
        };

        ParseDataSources(modelJson, modelNode);
        ParseSharedExpressions(modelJson, modelNode);
        ParseTables(modelJson, modelNode);
        ParseRelationships(modelJson, modelNode);
        ParsePerspectives(modelJson, modelNode);
        ParseCultures(modelJson, modelNode);
        ParseRoles(modelJson, modelNode);

        modelNode.BuildProperties();
        return modelNode;
    }

    /// <summary>
    /// Save the model tree back to a .bim file.
    /// </summary>
    public void SaveBimFile(string filePath, TomNode modelNode)
    {
        if (_rootJson == null)
        {
            _rootJson = new JObject
            {
                ["name"] = "SemanticModel",
                ["compatibilityLevel"] = 1600,
                ["model"] = modelNode.JsonObject ?? new JObject()
            };
        }
        else
        {
            _rootJson["model"] = modelNode.JsonObject ?? new JObject();
        }

        SyncNodeToJson(modelNode);

        var json = _rootJson.ToString(Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Synchronizes the TomNode tree changes back into the underlying JSON.
    /// </summary>
    private void SyncNodeToJson(TomNode node)
    {
        if (node.JsonObject != null)
        {
            node.JsonObject["name"] = node.Name;
            if (!string.IsNullOrEmpty(node.Description))
                node.JsonObject["description"] = node.Description;
            if (!string.IsNullOrEmpty(node.Expression))
                node.JsonObject["expression"] = node.Expression;
            if (!string.IsNullOrEmpty(node.FormatString))
                node.JsonObject["formatString"] = node.FormatString;
            if (node.IsHidden)
                node.JsonObject["isHidden"] = true;
            else
                node.JsonObject.Remove("isHidden");
        }

        foreach (var child in node.Children)
        {
            SyncNodeToJson(child);
        }
    }

    private void ParseDataSources(JObject modelJson, TomNode parent)
    {
        var dataSources = modelJson["dataSources"] as JArray;
        if (dataSources == null || dataSources.Count == 0) return;

        var folder = new TomNode("Data Sources", TomObjectType.DataSources, parent);
        parent.Children.Add(folder);

        foreach (var ds in dataSources)
        {
            var dsObj = ds as JObject;
            if (dsObj == null) continue;

            var dsNode = new TomNode(
                dsObj["name"]?.ToString() ?? "Unknown Data Source",
                TomObjectType.DataSource, folder)
            {
                JsonObject = dsObj,
                Description = dsObj["description"]?.ToString() ?? string.Empty
            };
            dsNode.BuildProperties();
            folder.Children.Add(dsNode);
        }
    }

    private void ParseSharedExpressions(JObject modelJson, TomNode parent)
    {
        var expressions = modelJson["expressions"] as JArray;
        if (expressions == null || expressions.Count == 0) return;

        var folder = new TomNode("Shared Expressions", TomObjectType.SharedExpressions, parent);
        parent.Children.Add(folder);

        foreach (var expr in expressions)
        {
            var exprObj = expr as JObject;
            if (exprObj == null) continue;

            var exprNode = new TomNode(
                exprObj["name"]?.ToString() ?? "Unknown Expression",
                TomObjectType.SharedExpression, folder)
            {
                JsonObject = exprObj,
                Expression = exprObj["expression"]?.ToString() ?? string.Empty,
                Description = exprObj["description"]?.ToString() ?? string.Empty
            };
            exprNode.BuildProperties();
            folder.Children.Add(exprNode);
        }
    }

    private void ParseTables(JObject modelJson, TomNode parent)
    {
        var tables = modelJson["tables"] as JArray;
        if (tables == null) return;

        var tablesFolder = new TomNode("Tables", TomObjectType.Tables, parent) { IsExpanded = true };
        parent.Children.Add(tablesFolder);

        foreach (var table in tables)
        {
            var tableObj = table as JObject;
            if (tableObj == null) continue;

            var tableNode = new TomNode(
                tableObj["name"]?.ToString() ?? "Unknown Table",
                TomObjectType.Table, tablesFolder)
            {
                JsonObject = tableObj,
                Description = tableObj["description"]?.ToString() ?? string.Empty,
                IsHidden = tableObj["isHidden"]?.Value<bool>() ?? false
            };

            ParseColumns(tableObj, tableNode);
            ParseMeasures(tableObj, tableNode);
            ParseHierarchies(tableObj, tableNode);
            ParsePartitions(tableObj, tableNode);

            tableNode.BuildProperties();
            tablesFolder.Children.Add(tableNode);
        }
    }

    private void ParseColumns(JObject tableObj, TomNode tableNode)
    {
        var columns = tableObj["columns"] as JArray;
        if (columns == null || columns.Count == 0) return;

        var folder = new TomNode("Columns", TomObjectType.Columns, tableNode);
        tableNode.Children.Add(folder);

        foreach (var col in columns)
        {
            var colObj = col as JObject;
            if (colObj == null) continue;

            var colType = colObj["type"]?.ToString();
            var objectType = colType switch
            {
                "calculated" => TomObjectType.CalculatedColumn,
                "calculatedTableColumn" => TomObjectType.CalculatedTableColumn,
                _ => TomObjectType.DataColumn
            };

            var colNode = new TomNode(
                colObj["name"]?.ToString() ?? "Unknown Column",
                objectType, folder)
            {
                JsonObject = colObj,
                DataType = colObj["dataType"]?.ToString() ?? string.Empty,
                Expression = colObj["expression"]?.ToString() ?? string.Empty,
                FormatString = colObj["formatString"]?.ToString() ?? string.Empty,
                IsHidden = colObj["isHidden"]?.Value<bool>() ?? false,
                DisplayFolder = colObj["displayFolder"]?.ToString() ?? string.Empty,
                Description = colObj["description"]?.ToString() ?? string.Empty
            };
            colNode.BuildProperties();
            folder.Children.Add(colNode);
        }
    }

    private void ParseMeasures(JObject tableObj, TomNode tableNode)
    {
        var measures = tableObj["measures"] as JArray;
        if (measures == null || measures.Count == 0) return;

        var folder = new TomNode("Measures", TomObjectType.Measures, tableNode);
        tableNode.Children.Add(folder);

        foreach (var meas in measures)
        {
            var measObj = meas as JObject;
            if (measObj == null) continue;

            // Handle expression that might be string or array of strings
            var expression = string.Empty;
            var exprToken = measObj["expression"];
            if (exprToken != null)
            {
                if (exprToken.Type == JTokenType.Array)
                {
                    var lines = exprToken.ToObject<string[]>();
                    expression = lines != null ? string.Join("\n", lines) : string.Empty;
                }
                else
                {
                    expression = exprToken.ToString();
                }
            }

            var measNode = new TomNode(
                measObj["name"]?.ToString() ?? "Unknown Measure",
                TomObjectType.Measure, folder)
            {
                JsonObject = measObj,
                Expression = expression,
                FormatString = measObj["formatString"]?.ToString() ?? string.Empty,
                IsHidden = measObj["isHidden"]?.Value<bool>() ?? false,
                DisplayFolder = measObj["displayFolder"]?.ToString() ?? string.Empty,
                Description = measObj["description"]?.ToString() ?? string.Empty
            };
            measNode.BuildProperties();
            folder.Children.Add(measNode);
        }
    }

    private void ParseHierarchies(JObject tableObj, TomNode tableNode)
    {
        var hierarchies = tableObj["hierarchies"] as JArray;
        if (hierarchies == null || hierarchies.Count == 0) return;

        var folder = new TomNode("Hierarchies", TomObjectType.Hierarchies, tableNode);
        tableNode.Children.Add(folder);

        foreach (var hier in hierarchies)
        {
            var hierObj = hier as JObject;
            if (hierObj == null) continue;

            var hierNode = new TomNode(
                hierObj["name"]?.ToString() ?? "Unknown Hierarchy",
                TomObjectType.Hierarchy, folder)
            {
                JsonObject = hierObj,
                Description = hierObj["description"]?.ToString() ?? string.Empty
            };

            var levels = hierObj["levels"] as JArray;
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    var levelObj = level as JObject;
                    if (levelObj == null) continue;

                    var levelNode = new TomNode(
                        levelObj["name"]?.ToString() ?? "Unknown Level",
                        TomObjectType.HierarchyLevel, hierNode)
                    {
                        JsonObject = levelObj
                    };
                    levelNode.BuildProperties();
                    hierNode.Children.Add(levelNode);
                }
            }

            hierNode.BuildProperties();
            folder.Children.Add(hierNode);
        }
    }

    private void ParsePartitions(JObject tableObj, TomNode tableNode)
    {
        var partitions = tableObj["partitions"] as JArray;
        if (partitions == null || partitions.Count == 0) return;

        var folder = new TomNode("Partitions", TomObjectType.Partitions, tableNode);
        tableNode.Children.Add(folder);

        foreach (var part in partitions)
        {
            var partObj = part as JObject;
            if (partObj == null) continue;

            var partNode = new TomNode(
                partObj["name"]?.ToString() ?? "Unknown Partition",
                TomObjectType.Partition, folder)
            {
                JsonObject = partObj,
                Description = partObj["description"]?.ToString() ?? string.Empty
            };
            partNode.BuildProperties();
            folder.Children.Add(partNode);
        }
    }

    private void ParseRelationships(JObject modelJson, TomNode parent)
    {
        var relationships = modelJson["relationships"] as JArray;
        if (relationships == null || relationships.Count == 0) return;

        var folder = new TomNode("Relationships", TomObjectType.Relationships, parent);
        parent.Children.Add(folder);

        foreach (var rel in relationships)
        {
            var relObj = rel as JObject;
            if (relObj == null) continue;

            var fromTable = relObj["fromTable"]?.ToString() ?? "?";
            var fromColumn = relObj["fromColumn"]?.ToString() ?? "?";
            var toTable = relObj["toTable"]?.ToString() ?? "?";
            var toColumn = relObj["toColumn"]?.ToString() ?? "?";
            var name = relObj["name"]?.ToString() ?? $"{fromTable}[{fromColumn}] -> {toTable}[{toColumn}]";

            var relNode = new TomNode(name, TomObjectType.Relationship, folder)
            {
                JsonObject = relObj,
                Description = $"{fromTable}[{fromColumn}] -> {toTable}[{toColumn}]"
            };
            relNode.BuildProperties();
            folder.Children.Add(relNode);
        }
    }

    private void ParsePerspectives(JObject modelJson, TomNode parent)
    {
        var perspectives = modelJson["perspectives"] as JArray;
        if (perspectives == null || perspectives.Count == 0) return;

        var folder = new TomNode("Perspectives", TomObjectType.Perspectives, parent);
        parent.Children.Add(folder);

        foreach (var persp in perspectives)
        {
            var perspObj = persp as JObject;
            if (perspObj == null) continue;

            var perspNode = new TomNode(
                perspObj["name"]?.ToString() ?? "Unknown Perspective",
                TomObjectType.Perspective, folder)
            {
                JsonObject = perspObj
            };
            perspNode.BuildProperties();
            folder.Children.Add(perspNode);
        }
    }

    private void ParseCultures(JObject modelJson, TomNode parent)
    {
        var cultures = modelJson["cultures"] as JArray;
        if (cultures == null || cultures.Count == 0) return;

        var folder = new TomNode("Cultures", TomObjectType.Cultures, parent);
        parent.Children.Add(folder);

        foreach (var culture in cultures)
        {
            var cultureObj = culture as JObject;
            if (cultureObj == null) continue;

            var cultureNode = new TomNode(
                cultureObj["name"]?.ToString() ?? "Unknown Culture",
                TomObjectType.Culture, folder)
            {
                JsonObject = cultureObj
            };
            cultureNode.BuildProperties();
            folder.Children.Add(cultureNode);
        }
    }

    private void ParseRoles(JObject modelJson, TomNode parent)
    {
        var roles = modelJson["roles"] as JArray;
        if (roles == null || roles.Count == 0) return;

        var folder = new TomNode("Roles", TomObjectType.Roles, parent);
        parent.Children.Add(folder);

        foreach (var role in roles)
        {
            var roleObj = role as JObject;
            if (roleObj == null) continue;

            var roleNode = new TomNode(
                roleObj["name"]?.ToString() ?? "Unknown Role",
                TomObjectType.Role, folder)
            {
                JsonObject = roleObj,
                Description = roleObj["description"]?.ToString() ?? string.Empty
            };
            roleNode.BuildProperties();
            folder.Children.Add(roleNode);
        }
    }

    /// <summary>
    /// Counts all objects in the model tree.
    /// </summary>
    public static int CountObjects(TomNode node)
    {
        int count = 1;
        foreach (var child in node.Children)
            count += CountObjects(child);
        return count;
    }

    /// <summary>
    /// Finds nodes matching a search text.
    /// </summary>
    public static void FilterTree(TomNode node, string searchText, bool showAll = false)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            SetVisibleRecursive(node, true);
            return;
        }

        var matches = node.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
                FilterTree(child, searchText);

            var anyChildVisible = node.Children.Any(c => c.IsVisible);
            node.IsVisible = matches || anyChildVisible;

            if (anyChildVisible)
                node.IsExpanded = true;
        }
        else
        {
            node.IsVisible = matches;
        }
    }

    private static void SetVisibleRecursive(TomNode node, bool visible)
    {
        node.IsVisible = visible;
        foreach (var child in node.Children)
            SetVisibleRecursive(child, visible);
    }
}
