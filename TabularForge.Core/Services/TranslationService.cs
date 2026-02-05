using System.Collections.ObjectModel;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

/// <summary>
/// Service for managing metadata translations (cultures) in a tabular model.
/// Reads/writes translation data from the TOM JSON structure.
/// </summary>
public class TranslationService
{
    /// <summary>
    /// Gets all culture codes defined in the model.
    /// </summary>
    public List<string> GetCultures(TomNode modelRoot)
    {
        var cultures = new List<string>();
        var culturesFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Cultures);
        if (culturesFolder == null) return cultures;

        foreach (var culture in culturesFolder.Children)
        {
            cultures.Add(culture.Name);
        }
        return cultures;
    }

    /// <summary>
    /// Builds translation entries for all translatable objects in the model.
    /// </summary>
    public List<TranslationEntry> BuildTranslationGrid(TomNode modelRoot)
    {
        var entries = new List<TranslationEntry>();
        var cultures = GetCultures(modelRoot);

        // Build a lookup: culture -> objectPath -> property -> translatedValue
        var translationLookup = BuildTranslationLookup(modelRoot);

        // Collect all translatable objects (tables, columns, measures, hierarchies)
        CollectTranslatableObjects(modelRoot, entries, string.Empty);

        // Fill in translation values from lookup
        foreach (var entry in entries)
        {
            foreach (var culture in cultures)
            {
                var key = $"{entry.ObjectPath}|{entry.Property}";
                var translated = string.Empty;
                if (translationLookup.TryGetValue(culture, out var cultureMap)
                    && cultureMap.TryGetValue(key, out var value))
                {
                    translated = value;
                }

                entry.Translations.Add(new CultureTranslation
                {
                    CultureCode = culture,
                    TranslatedValue = translated
                });
            }
        }

        return entries;
    }

    private Dictionary<string, Dictionary<string, string>> BuildTranslationLookup(TomNode modelRoot)
    {
        // Returns: cultureCode -> (objectPath|property -> translatedValue)
        var lookup = new Dictionary<string, Dictionary<string, string>>();

        var culturesFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Cultures);
        if (culturesFolder == null) return lookup;

        foreach (var cultureNode in culturesFolder.Children)
        {
            var cultureMap = new Dictionary<string, string>();
            lookup[cultureNode.Name] = cultureMap;

            var cultureJson = cultureNode.JsonObject;
            if (cultureJson == null) continue;

            var linguisticMetadata = cultureJson["linguisticMetadata"];
            var objectTranslations = cultureJson["translations"]?["model"] as JObject;
            if (objectTranslations == null) continue;

            // Parse table translations
            var tables = objectTranslations["tables"] as JArray;
            if (tables != null)
            {
                foreach (var table in tables)
                {
                    var tObj = table as JObject;
                    if (tObj == null) continue;
                    var tableName = tObj["name"]?.ToString() ?? "";

                    ExtractTranslation(cultureMap, tableName, "", "Caption",
                        tObj["translatedCaption"]?.ToString());
                    ExtractTranslation(cultureMap, tableName, "", "Description",
                        tObj["translatedDescription"]?.ToString());

                    // Columns
                    var columns = tObj["columns"] as JArray;
                    if (columns != null)
                    {
                        foreach (var col in columns)
                        {
                            var cObj = col as JObject;
                            if (cObj == null) continue;
                            var colName = cObj["name"]?.ToString() ?? "";
                            ExtractTranslation(cultureMap, tableName, colName, "Caption",
                                cObj["translatedCaption"]?.ToString());
                            ExtractTranslation(cultureMap, tableName, colName, "Description",
                                cObj["translatedDescription"]?.ToString());
                            ExtractTranslation(cultureMap, tableName, colName, "DisplayFolder",
                                cObj["translatedDisplayFolder"]?.ToString());
                        }
                    }

                    // Measures
                    var measures = tObj["measures"] as JArray;
                    if (measures != null)
                    {
                        foreach (var meas in measures)
                        {
                            var mObj = meas as JObject;
                            if (mObj == null) continue;
                            var measName = mObj["name"]?.ToString() ?? "";
                            ExtractTranslation(cultureMap, tableName, measName, "Caption",
                                mObj["translatedCaption"]?.ToString());
                            ExtractTranslation(cultureMap, tableName, measName, "Description",
                                mObj["translatedDescription"]?.ToString());
                            ExtractTranslation(cultureMap, tableName, measName, "DisplayFolder",
                                mObj["translatedDisplayFolder"]?.ToString());
                        }
                    }

                    // Hierarchies
                    var hierarchies = tObj["hierarchies"] as JArray;
                    if (hierarchies != null)
                    {
                        foreach (var hier in hierarchies)
                        {
                            var hObj = hier as JObject;
                            if (hObj == null) continue;
                            var hierName = hObj["name"]?.ToString() ?? "";
                            ExtractTranslation(cultureMap, tableName, hierName, "Caption",
                                hObj["translatedCaption"]?.ToString());
                            ExtractTranslation(cultureMap, tableName, hierName, "Description",
                                hObj["translatedDescription"]?.ToString());
                            ExtractTranslation(cultureMap, tableName, hierName, "DisplayFolder",
                                hObj["translatedDisplayFolder"]?.ToString());
                        }
                    }
                }
            }
        }

        return lookup;
    }

    private void ExtractTranslation(Dictionary<string, string> map,
        string tableName, string objectName, string property, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var path = string.IsNullOrEmpty(objectName)
            ? $"{tableName}|{property}"
            : $"{tableName}\\{objectName}|{property}";
        map[path] = value;
    }

    private void CollectTranslatableObjects(TomNode node, List<TranslationEntry> entries, string parentTable)
    {
        switch (node.ObjectType)
        {
            case TomObjectType.Table:
                entries.Add(new TranslationEntry
                {
                    ObjectName = node.Name,
                    ObjectType = "Table",
                    TableName = node.Name,
                    Property = "Caption",
                    DefaultValue = node.Name,
                    ObjectPath = node.Name
                });
                entries.Add(new TranslationEntry
                {
                    ObjectName = node.Name,
                    ObjectType = "Table",
                    TableName = node.Name,
                    Property = "Description",
                    DefaultValue = node.Description,
                    ObjectPath = node.Name
                });
                foreach (var child in node.Children)
                    CollectTranslatableObjects(child, entries, node.Name);
                return;

            case TomObjectType.DataColumn:
            case TomObjectType.Column:
            case TomObjectType.CalculatedColumn:
            case TomObjectType.CalculatedTableColumn:
                AddObjectTranslationEntries(entries, node, "Column", parentTable);
                break;

            case TomObjectType.Measure:
                AddObjectTranslationEntries(entries, node, "Measure", parentTable);
                break;

            case TomObjectType.Hierarchy:
                AddObjectTranslationEntries(entries, node, "Hierarchy", parentTable);
                break;
        }

        foreach (var child in node.Children)
            CollectTranslatableObjects(child, entries, parentTable);
    }

    private void AddObjectTranslationEntries(List<TranslationEntry> entries,
        TomNode node, string objectType, string tableName)
    {
        var path = $"{tableName}\\{node.Name}";
        entries.Add(new TranslationEntry
        {
            ObjectName = node.Name,
            ObjectType = objectType,
            TableName = tableName,
            Property = "Caption",
            DefaultValue = node.Name,
            ObjectPath = path
        });
        entries.Add(new TranslationEntry
        {
            ObjectName = node.Name,
            ObjectType = objectType,
            TableName = tableName,
            Property = "Description",
            DefaultValue = node.Description,
            ObjectPath = path
        });
        entries.Add(new TranslationEntry
        {
            ObjectName = node.Name,
            ObjectType = objectType,
            TableName = tableName,
            Property = "DisplayFolder",
            DefaultValue = node.DisplayFolder,
            ObjectPath = path
        });
    }

    /// <summary>
    /// Saves translation changes back to the model JSON.
    /// </summary>
    public void SaveTranslations(TomNode modelRoot, List<TranslationEntry> entries)
    {
        var culturesFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Cultures);
        if (culturesFolder == null) return;

        foreach (var cultureNode in culturesFolder.Children)
        {
            var cultureJson = cultureNode.JsonObject;
            if (cultureJson == null) continue;

            // Ensure translations.model structure exists
            if (cultureJson["translations"] == null)
                cultureJson["translations"] = new JObject();
            var translations = cultureJson["translations"] as JObject;
            if (translations!["model"] == null)
                translations["model"] = new JObject();
            var modelObj = translations["model"] as JObject;

            // Build table translation groups
            var tableGroups = entries
                .Where(e => e.Translations.Any(t => t.CultureCode == cultureNode.Name
                    && !string.IsNullOrEmpty(t.TranslatedValue)))
                .GroupBy(e => e.TableName);

            var tablesArray = new JArray();
            foreach (var group in tableGroups)
            {
                var tableJson = new JObject { ["name"] = group.Key };

                var tableEntries = group.Where(e => e.ObjectType == "Table").ToList();
                foreach (var te in tableEntries)
                {
                    var trans = te.Translations.FirstOrDefault(t => t.CultureCode == cultureNode.Name);
                    if (trans != null && !string.IsNullOrEmpty(trans.TranslatedValue))
                    {
                        var propKey = te.Property switch
                        {
                            "Caption" => "translatedCaption",
                            "Description" => "translatedDescription",
                            _ => null
                        };
                        if (propKey != null) tableJson[propKey] = trans.TranslatedValue;
                    }
                }

                WriteChildTranslations(tableJson, "columns", group, "Column", cultureNode.Name);
                WriteChildTranslations(tableJson, "measures", group, "Measure", cultureNode.Name);
                WriteChildTranslations(tableJson, "hierarchies", group, "Hierarchy", cultureNode.Name);

                tablesArray.Add(tableJson);
            }

            if (tablesArray.Count > 0)
                modelObj!["tables"] = tablesArray;
        }
    }

    private void WriteChildTranslations(JObject tableJson, string arrayName,
        IGrouping<string, TranslationEntry> group, string objectType, string culture)
    {
        var children = group.Where(e => e.ObjectType == objectType).ToList();
        if (children.Count == 0) return;

        var childGroups = children.GroupBy(e => e.ObjectName);
        var array = new JArray();

        foreach (var childGroup in childGroups)
        {
            var childJson = new JObject { ["name"] = childGroup.Key };
            foreach (var entry in childGroup)
            {
                var trans = entry.Translations.FirstOrDefault(t => t.CultureCode == culture);
                if (trans != null && !string.IsNullOrEmpty(trans.TranslatedValue))
                {
                    var propKey = entry.Property switch
                    {
                        "Caption" => "translatedCaption",
                        "Description" => "translatedDescription",
                        "DisplayFolder" => "translatedDisplayFolder",
                        _ => null
                    };
                    if (propKey != null) childJson[propKey] = trans.TranslatedValue;
                }
            }
            if (childJson.Properties().Count() > 1)
                array.Add(childJson);
        }

        if (array.Count > 0)
            tableJson[arrayName] = array;
    }

    /// <summary>
    /// Adds a new culture to the model.
    /// </summary>
    public TomNode AddCulture(TomNode modelRoot, string cultureCode)
    {
        var culturesFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Cultures);
        if (culturesFolder == null)
        {
            culturesFolder = new TomNode("Cultures", TomObjectType.Cultures, modelRoot);
            modelRoot.Children.Add(culturesFolder);

            // Add to model JSON
            if (modelRoot.JsonObject != null && modelRoot.JsonObject["cultures"] == null)
                modelRoot.JsonObject["cultures"] = new JArray();
        }

        var cultureJson = new JObject
        {
            ["name"] = cultureCode,
            ["translations"] = new JObject
            {
                ["model"] = new JObject
                {
                    ["tables"] = new JArray()
                }
            }
        };

        var cultureNode = new TomNode(cultureCode, TomObjectType.Culture, culturesFolder)
        {
            JsonObject = cultureJson
        };
        culturesFolder.Children.Add(cultureNode);

        // Add to model JSON array
        var culturesArray = modelRoot.JsonObject?["cultures"] as JArray;
        culturesArray?.Add(cultureJson);

        return cultureNode;
    }

    /// <summary>
    /// Removes a culture from the model.
    /// </summary>
    public void RemoveCulture(TomNode modelRoot, string cultureCode)
    {
        var culturesFolder = modelRoot.Children
            .FirstOrDefault(c => c.ObjectType == TomObjectType.Cultures);
        if (culturesFolder == null) return;

        var cultureNode = culturesFolder.Children
            .FirstOrDefault(c => c.Name == cultureCode);
        if (cultureNode == null) return;

        culturesFolder.Children.Remove(cultureNode);

        // Remove from model JSON
        var culturesArray = modelRoot.JsonObject?["cultures"] as JArray;
        if (culturesArray != null)
        {
            var toRemove = culturesArray
                .FirstOrDefault(c => c["name"]?.ToString() == cultureCode);
            if (toRemove != null)
                culturesArray.Remove(toRemove);
        }
    }

    /// <summary>
    /// Exports translations to CSV.
    /// </summary>
    public string ExportToCsv(List<TranslationEntry> entries, List<string> cultures)
    {
        var sb = new StringBuilder();
        sb.Append("Table,Object,Type,Property,Default");
        foreach (var culture in cultures)
            sb.Append($",{culture}");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            sb.Append($"\"{entry.TableName}\",\"{entry.ObjectName}\",\"{entry.ObjectType}\",\"{entry.Property}\",\"{entry.DefaultValue}\"");
            foreach (var culture in cultures)
            {
                var trans = entry.Translations.FirstOrDefault(t => t.CultureCode == culture);
                sb.Append($",\"{trans?.TranslatedValue ?? ""}\"");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports translations to JSON.
    /// </summary>
    public string ExportToJson(List<TranslationEntry> entries, List<string> cultures)
    {
        var result = new JObject();
        foreach (var culture in cultures)
        {
            var cultureObj = new JArray();
            foreach (var entry in entries)
            {
                var trans = entry.Translations.FirstOrDefault(t => t.CultureCode == culture);
                if (trans != null && !string.IsNullOrEmpty(trans.TranslatedValue))
                {
                    cultureObj.Add(new JObject
                    {
                        ["table"] = entry.TableName,
                        ["object"] = entry.ObjectName,
                        ["type"] = entry.ObjectType,
                        ["property"] = entry.Property,
                        ["value"] = trans.TranslatedValue
                    });
                }
            }
            result[culture] = cultureObj;
        }
        return result.ToString(Formatting.Indented);
    }

    /// <summary>
    /// Imports translations from JSON.
    /// </summary>
    public void ImportFromJson(string json, List<TranslationEntry> entries)
    {
        var root = JObject.Parse(json);
        foreach (var cultureProp in root.Properties())
        {
            var culture = cultureProp.Name;
            var translations = cultureProp.Value as JArray;
            if (translations == null) continue;

            foreach (var trans in translations)
            {
                var table = trans["table"]?.ToString();
                var obj = trans["object"]?.ToString();
                var prop = trans["property"]?.ToString();
                var value = trans["value"]?.ToString();

                var entry = entries.FirstOrDefault(e =>
                    e.TableName == table && e.ObjectName == obj && e.Property == prop);
                if (entry != null)
                {
                    var ct = entry.Translations.FirstOrDefault(t => t.CultureCode == culture);
                    if (ct != null)
                    {
                        ct.TranslatedValue = value ?? string.Empty;
                        ct.IsModified = true;
                    }
                }
            }
        }
    }
}
