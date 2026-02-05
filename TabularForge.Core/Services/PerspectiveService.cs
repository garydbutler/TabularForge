using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

/// <summary>
/// Service for managing perspective definitions and membership in a tabular model.
/// </summary>
public class PerspectiveService
{
    /// <summary>
    /// Gets all perspective names defined in the model.
    /// </summary>
    public List<string> GetPerspectives(TomNode modelRoot)
    {
        var perspectives = new List<string>();
        var folder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Perspectives);
        if (folder == null) return perspectives;

        foreach (var persp in folder.Children)
            perspectives.Add(persp.Name);

        return perspectives;
    }

    /// <summary>
    /// Builds the perspective membership grid for all objects.
    /// </summary>
    public List<PerspectiveMembership> BuildMembershipGrid(TomNode modelRoot)
    {
        var memberships = new List<PerspectiveMembership>();
        var perspectives = GetPerspectives(modelRoot);

        // Build lookup: perspectiveName -> set of included object paths
        var inclusionLookup = BuildInclusionLookup(modelRoot);

        // Collect all perspectable objects
        CollectPerspectableObjects(modelRoot, memberships, string.Empty);

        // Fill inclusions
        foreach (var membership in memberships)
        {
            foreach (var persp in perspectives)
            {
                var isIncluded = false;
                if (inclusionLookup.TryGetValue(persp, out var includedObjects))
                {
                    isIncluded = includedObjects.Contains(membership.ObjectPath);
                }

                membership.Inclusions.Add(new PerspectiveInclusion
                {
                    PerspectiveName = persp,
                    IsIncluded = isIncluded
                });
            }
        }

        return memberships;
    }

    private Dictionary<string, HashSet<string>> BuildInclusionLookup(TomNode modelRoot)
    {
        var lookup = new Dictionary<string, HashSet<string>>();

        var perspFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Perspectives);
        if (perspFolder == null) return lookup;

        foreach (var perspNode in perspFolder.Children)
        {
            var included = new HashSet<string>();
            lookup[perspNode.Name] = included;

            var perspJson = perspNode.JsonObject;
            if (perspJson == null) continue;

            var tables = perspJson["tables"] as JArray;
            if (tables == null) continue;

            foreach (var table in tables)
            {
                var tObj = table as JObject;
                if (tObj == null) continue;
                var tableName = tObj["name"]?.ToString() ?? "";
                included.Add(tableName); // Table itself is included

                // Columns
                var columns = tObj["columns"] as JArray;
                if (columns != null)
                {
                    foreach (var col in columns)
                    {
                        var colName = col["name"]?.ToString() ?? "";
                        included.Add($"{tableName}\\{colName}");
                    }
                }

                // Measures
                var measures = tObj["measures"] as JArray;
                if (measures != null)
                {
                    foreach (var meas in measures)
                    {
                        var measName = meas["name"]?.ToString() ?? "";
                        included.Add($"{tableName}\\{measName}");
                    }
                }

                // Hierarchies
                var hierarchies = tObj["hierarchies"] as JArray;
                if (hierarchies != null)
                {
                    foreach (var hier in hierarchies)
                    {
                        var hierName = hier["name"]?.ToString() ?? "";
                        included.Add($"{tableName}\\{hierName}");
                    }
                }
            }
        }

        return lookup;
    }

    private void CollectPerspectableObjects(TomNode node,
        List<PerspectiveMembership> memberships, string parentTable)
    {
        switch (node.ObjectType)
        {
            case TomObjectType.Table:
                memberships.Add(new PerspectiveMembership
                {
                    ObjectName = node.Name,
                    ObjectType = "Table",
                    TableName = node.Name,
                    ObjectPath = node.Name
                });
                foreach (var child in node.Children)
                    CollectPerspectableObjects(child, memberships, node.Name);
                return;

            case TomObjectType.DataColumn:
            case TomObjectType.Column:
            case TomObjectType.CalculatedColumn:
            case TomObjectType.CalculatedTableColumn:
                memberships.Add(new PerspectiveMembership
                {
                    ObjectName = node.Name,
                    ObjectType = "Column",
                    TableName = parentTable,
                    ObjectPath = $"{parentTable}\\{node.Name}"
                });
                break;

            case TomObjectType.Measure:
                memberships.Add(new PerspectiveMembership
                {
                    ObjectName = node.Name,
                    ObjectType = "Measure",
                    TableName = parentTable,
                    ObjectPath = $"{parentTable}\\{node.Name}"
                });
                break;

            case TomObjectType.Hierarchy:
                memberships.Add(new PerspectiveMembership
                {
                    ObjectName = node.Name,
                    ObjectType = "Hierarchy",
                    TableName = parentTable,
                    ObjectPath = $"{parentTable}\\{node.Name}"
                });
                break;
        }

        foreach (var child in node.Children)
            CollectPerspectableObjects(child, memberships, parentTable);
    }

    /// <summary>
    /// Saves perspective membership changes back to the model JSON.
    /// </summary>
    public void SaveMemberships(TomNode modelRoot, List<PerspectiveMembership> memberships)
    {
        var perspFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Perspectives);
        if (perspFolder == null) return;

        foreach (var perspNode in perspFolder.Children)
        {
            var perspJson = perspNode.JsonObject;
            if (perspJson == null) continue;

            // Build table structure for this perspective
            var tablesArray = new JArray();
            var tableGroups = memberships
                .Where(m => m.Inclusions.Any(i => i.PerspectiveName == perspNode.Name && i.IsIncluded))
                .GroupBy(m => m.TableName);

            foreach (var group in tableGroups)
            {
                var tableJson = new JObject { ["name"] = group.Key };

                var columns = group.Where(m => m.ObjectType == "Column").ToList();
                if (columns.Count > 0)
                {
                    var colArray = new JArray();
                    foreach (var col in columns)
                        colArray.Add(new JObject { ["name"] = col.ObjectName });
                    tableJson["columns"] = colArray;
                }

                var measures = group.Where(m => m.ObjectType == "Measure").ToList();
                if (measures.Count > 0)
                {
                    var measArray = new JArray();
                    foreach (var meas in measures)
                        measArray.Add(new JObject { ["name"] = meas.ObjectName });
                    tableJson["measures"] = measArray;
                }

                var hierarchies = group.Where(m => m.ObjectType == "Hierarchy").ToList();
                if (hierarchies.Count > 0)
                {
                    var hierArray = new JArray();
                    foreach (var hier in hierarchies)
                        hierArray.Add(new JObject { ["name"] = hier.ObjectName });
                    tableJson["hierarchies"] = hierArray;
                }

                tablesArray.Add(tableJson);
            }

            perspJson["tables"] = tablesArray;
        }
    }

    /// <summary>
    /// Adds a new perspective to the model.
    /// </summary>
    public TomNode AddPerspective(TomNode modelRoot, string name)
    {
        var perspFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Perspectives);
        if (perspFolder == null)
        {
            perspFolder = new TomNode("Perspectives", TomObjectType.Perspectives, modelRoot);
            modelRoot.Children.Add(perspFolder);

            if (modelRoot.JsonObject != null && modelRoot.JsonObject["perspectives"] == null)
                modelRoot.JsonObject["perspectives"] = new JArray();
        }

        var perspJson = new JObject
        {
            ["name"] = name,
            ["tables"] = new JArray()
        };

        var perspNode = new TomNode(name, TomObjectType.Perspective, perspFolder)
        {
            JsonObject = perspJson
        };
        perspFolder.Children.Add(perspNode);

        var perspArray = modelRoot.JsonObject?["perspectives"] as JArray;
        perspArray?.Add(perspJson);

        return perspNode;
    }

    /// <summary>
    /// Removes a perspective from the model.
    /// </summary>
    public void RemovePerspective(TomNode modelRoot, string name)
    {
        var perspFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Perspectives);
        if (perspFolder == null) return;

        var perspNode = perspFolder.Children.FirstOrDefault(p => p.Name == name);
        if (perspNode == null) return;

        perspFolder.Children.Remove(perspNode);

        var perspArray = modelRoot.JsonObject?["perspectives"] as JArray;
        if (perspArray != null)
        {
            var toRemove = perspArray.FirstOrDefault(p => p["name"]?.ToString() == name);
            if (toRemove != null) perspArray.Remove(toRemove);
        }
    }

    /// <summary>
    /// Sets all objects in a perspective to included or excluded.
    /// </summary>
    public void BulkSetPerspective(List<PerspectiveMembership> memberships,
        string perspectiveName, bool included)
    {
        foreach (var membership in memberships)
        {
            var inclusion = membership.Inclusions
                .FirstOrDefault(i => i.PerspectiveName == perspectiveName);
            if (inclusion != null)
                inclusion.IsIncluded = included;
        }
    }

    /// <summary>
    /// Sets all objects of a specific type in a perspective.
    /// </summary>
    public void BulkSetByType(List<PerspectiveMembership> memberships,
        string perspectiveName, string objectType, bool included)
    {
        foreach (var membership in memberships.Where(m => m.ObjectType == objectType))
        {
            var inclusion = membership.Inclusions
                .FirstOrDefault(i => i.PerspectiveName == perspectiveName);
            if (inclusion != null)
                inclusion.IsIncluded = included;
        }
    }
}
