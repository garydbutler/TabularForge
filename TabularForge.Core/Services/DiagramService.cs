using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TabularForge.Core.Models;

namespace TabularForge.Core.Services;

public class DiagramService
{
    private readonly BimFileService _bimFileService;

    public DiagramService(BimFileService bimFileService)
    {
        _bimFileService = bimFileService;
    }

    /// <summary>
    /// Extract table cards from the TOM model tree.
    /// </summary>
    public List<DiagramTableCard> ExtractTableCards(TomNode? modelRoot)
    {
        var cards = new List<DiagramTableCard>();
        if (modelRoot == null) return cards;

        CollectTables(modelRoot, cards);
        return cards;
    }

    private void CollectTables(TomNode node, List<DiagramTableCard> cards)
    {
        if (node.ObjectType == TomObjectType.Table)
        {
            var card = new DiagramTableCard
            {
                TableName = node.Name
            };

            foreach (var child in node.Children)
            {
                CollectCardMembers(child, card);
            }

            cards.Add(card);
        }

        foreach (var child in node.Children)
        {
            CollectTables(child, cards);
        }
    }

    private void CollectCardMembers(TomNode node, DiagramTableCard card)
    {
        switch (node.ObjectType)
        {
            case TomObjectType.DataColumn:
            case TomObjectType.Column:
            case TomObjectType.CalculatedColumn:
            case TomObjectType.CalculatedTableColumn:
                card.Columns.Add(new DiagramColumn
                {
                    Name = node.Name,
                    DataType = node.DataType,
                    TableName = card.TableName
                });
                break;

            case TomObjectType.Measure:
                card.Measures.Add(new DiagramMeasure
                {
                    Name = node.Name,
                    Expression = node.Expression
                });
                break;
        }

        foreach (var child in node.Children)
        {
            CollectCardMembers(child, card);
        }
    }

    /// <summary>
    /// Extract relationships from the TOM model tree.
    /// </summary>
    public List<DiagramRelationship> ExtractRelationships(TomNode? modelRoot)
    {
        var relationships = new List<DiagramRelationship>();
        if (modelRoot == null) return relationships;

        CollectRelationships(modelRoot, relationships);
        return relationships;
    }

    private void CollectRelationships(TomNode node, List<DiagramRelationship> relationships)
    {
        if (node.ObjectType == TomObjectType.Relationship && node.JsonObject != null)
        {
            var rel = new DiagramRelationship
            {
                FromTable = node.JsonObject["fromTable"]?.ToString() ?? "",
                FromColumn = node.JsonObject["fromColumn"]?.ToString() ?? "",
                ToTable = node.JsonObject["toTable"]?.ToString() ?? "",
                ToColumn = node.JsonObject["toColumn"]?.ToString() ?? "",
                IsActive = node.JsonObject["isActive"]?.ToObject<bool>() ?? true,
            };

            // Parse cardinality from fromCardinality/toCardinality
            var fromCard = node.JsonObject["fromCardinality"]?.ToString() ?? "many";
            var toCard = node.JsonObject["toCardinality"]?.ToString() ?? "one";
            rel.Cardinality = (fromCard, toCard) switch
            {
                ("one", "many") => Cardinality.OneToMany,
                ("many", "one") => Cardinality.ManyToOne,
                ("one", "one") => Cardinality.OneToOne,
                ("many", "many") => Cardinality.ManyToMany,
                _ => Cardinality.ManyToOne
            };

            // Parse cross-filter direction
            var cfDir = node.JsonObject["crossFilteringBehavior"]?.ToString() ?? "oneDirection";
            rel.CrossFilterDirection = cfDir == "bothDirections"
                ? CrossFilterDirection.Both
                : CrossFilterDirection.Single;

            relationships.Add(rel);
        }

        foreach (var child in node.Children)
        {
            CollectRelationships(child, relationships);
        }
    }

    /// <summary>
    /// Mark columns that are used as keys in relationships.
    /// </summary>
    public void MarkKeyColumns(List<DiagramTableCard> tables, List<DiagramRelationship> relationships)
    {
        foreach (var rel in relationships)
        {
            var fromTable = tables.FirstOrDefault(t => t.TableName == rel.FromTable);
            var toTable = tables.FirstOrDefault(t => t.TableName == rel.ToTable);

            var fromCol = fromTable?.Columns.FirstOrDefault(c => c.Name == rel.FromColumn);
            var toCol = toTable?.Columns.FirstOrDefault(c => c.Name == rel.ToColumn);

            if (fromCol != null) fromCol.IsForeignKey = true;
            if (toCol != null) toCol.IsPrimaryKey = true;
        }
    }

    /// <summary>
    /// Auto-layout tables in a tree/grid pattern.
    /// </summary>
    public void ApplyTreeLayout(List<DiagramTableCard> tables, double canvasWidth)
    {
        const double cardWidth = 220;
        const double cardHeight = 280;
        const double hGap = 40;
        const double vGap = 40;
        const double startX = 40;
        const double startY = 40;

        int cols = Math.Max(1, (int)((canvasWidth - startX) / (cardWidth + hGap)));

        for (int i = 0; i < tables.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            tables[i].X = startX + col * (cardWidth + hGap);
            tables[i].Y = startY + row * (cardHeight + vGap);
        }
    }

    /// <summary>
    /// Simple force-directed layout for diagram.
    /// </summary>
    public void ApplyForceDirectedLayout(List<DiagramTableCard> tables,
        List<DiagramRelationship> relationships, int iterations = 100)
    {
        var rng = new Random(42);

        // Initialize positions randomly if not set
        foreach (var t in tables)
        {
            if (t.X == 0 && t.Y == 0)
            {
                t.X = rng.NextDouble() * 1200 + 50;
                t.Y = rng.NextDouble() * 800 + 50;
            }
        }

        var lookup = tables.ToDictionary(t => t.TableName);
        double k = 300; // optimal distance
        double temp = 200;

        for (int iter = 0; iter < iterations; iter++)
        {
            var forces = tables.ToDictionary(t => t.TableName, _ => (fx: 0.0, fy: 0.0));

            // Repulsive forces between all pairs
            for (int i = 0; i < tables.Count; i++)
            {
                for (int j = i + 1; j < tables.Count; j++)
                {
                    double dx = tables[i].X - tables[j].X;
                    double dy = tables[i].Y - tables[j].Y;
                    double dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1);
                    double force = k * k / dist;
                    double fx = force * dx / dist;
                    double fy = force * dy / dist;

                    forces[tables[i].TableName] = (forces[tables[i].TableName].fx + fx,
                        forces[tables[i].TableName].fy + fy);
                    forces[tables[j].TableName] = (forces[tables[j].TableName].fx - fx,
                        forces[tables[j].TableName].fy - fy);
                }
            }

            // Attractive forces along relationships
            foreach (var rel in relationships)
            {
                if (!lookup.ContainsKey(rel.FromTable) || !lookup.ContainsKey(rel.ToTable))
                    continue;

                var from = lookup[rel.FromTable];
                var to = lookup[rel.ToTable];
                double dx = from.X - to.X;
                double dy = from.Y - to.Y;
                double dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1);
                double force = dist * dist / k;
                double fx = force * dx / dist;
                double fy = force * dy / dist;

                forces[from.TableName] = (forces[from.TableName].fx - fx, forces[from.TableName].fy - fy);
                forces[to.TableName] = (forces[to.TableName].fx + fx, forces[to.TableName].fy + fy);
            }

            // Apply forces with cooling
            foreach (var t in tables)
            {
                var f = forces[t.TableName];
                double magnitude = Math.Max(Math.Sqrt(f.fx * f.fx + f.fy * f.fy), 1);
                double capped = Math.Min(magnitude, temp);
                t.X += f.fx / magnitude * capped;
                t.Y += f.fy / magnitude * capped;

                // Keep in bounds
                t.X = Math.Max(20, t.X);
                t.Y = Math.Max(20, t.Y);
            }

            temp *= 0.95;
        }
    }

    /// <summary>
    /// Apply hierarchical layout based on relationship direction.
    /// </summary>
    public void ApplyHierarchicalLayout(List<DiagramTableCard> tables,
        List<DiagramRelationship> relationships)
    {
        // Build adjacency for "to" side (fact tables point to dimension tables)
        var incoming = new Dictionary<string, int>();
        foreach (var t in tables)
            incoming[t.TableName] = 0;

        foreach (var rel in relationships)
        {
            if (incoming.ContainsKey(rel.FromTable))
                incoming[rel.FromTable]++;
        }

        // Sort by incoming count (dimension tables first, then fact tables)
        var sorted = tables.OrderBy(t => incoming.GetValueOrDefault(t.TableName, 0)).ToList();

        // Assign levels based on relationship depth
        var levels = new Dictionary<string, int>();
        var visited = new HashSet<string>();
        var adjacency = new Dictionary<string, List<string>>();

        foreach (var t in tables)
            adjacency[t.TableName] = new();

        foreach (var rel in relationships)
        {
            if (adjacency.ContainsKey(rel.ToTable))
                adjacency[rel.ToTable].Add(rel.FromTable);
        }

        // BFS to assign levels
        var queue = new Queue<string>();
        foreach (var t in sorted.Where(t => incoming.GetValueOrDefault(t.TableName, 0) == 0))
        {
            levels[t.TableName] = 0;
            visited.Add(t.TableName);
            queue.Enqueue(t.TableName);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in adjacency.GetValueOrDefault(current, new()))
            {
                if (visited.Add(neighbor))
                {
                    levels[neighbor] = levels[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Assign remaining unvisited tables
        foreach (var t in tables.Where(t => !visited.Contains(t.TableName)))
            levels[t.TableName] = 0;

        // Position tables by level
        const double hGap = 280;
        const double vGap = 320;
        const double startX = 40;
        const double startY = 40;

        var levelGroups = tables.GroupBy(t => levels.GetValueOrDefault(t.TableName, 0))
            .OrderBy(g => g.Key);

        foreach (var group in levelGroups)
        {
            int col = 0;
            foreach (var table in group)
            {
                table.X = startX + group.Key * hGap;
                table.Y = startY + col * vGap;
                col++;
            }
        }
    }

    /// <summary>
    /// Snap a position to the nearest grid point.
    /// </summary>
    public static double SnapToGrid(double value, double gridSize)
    {
        return Math.Round(value / gridSize) * gridSize;
    }

    /// <summary>
    /// Save diagram layout to JSON string.
    /// </summary>
    public string SaveLayout(DiagramLayout layout)
    {
        return JsonConvert.SerializeObject(layout, Formatting.Indented);
    }

    /// <summary>
    /// Load diagram layout from JSON string.
    /// </summary>
    public DiagramLayout? LoadLayout(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<DiagramLayout>(json);
        }
        catch
        {
            return null;
        }
    }
}
