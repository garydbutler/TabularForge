using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using TabularForge.Core.Models;

namespace TabularForge.UI.Converters;

/// <summary>
/// Converts a TomObjectType to a colored geometry icon for the tree view.
/// Uses simple geometric shapes as vector icons.
/// </summary>
public class ObjectTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TomObjectType objType)
            return GetIconGeometry("folder");

        return objType switch
        {
            TomObjectType.Model => GetIconGeometry("model"),
            TomObjectType.DataSources or TomObjectType.Tables or TomObjectType.Columns
                or TomObjectType.Measures or TomObjectType.Hierarchies or TomObjectType.Partitions
                or TomObjectType.Relationships or TomObjectType.Perspectives or TomObjectType.Cultures
                or TomObjectType.Roles or TomObjectType.CalculationGroups or TomObjectType.SharedExpressions
                or TomObjectType.Annotations or TomObjectType.Folder
                => GetIconGeometry("folder"),
            TomObjectType.DataSource => GetIconGeometry("datasource"),
            TomObjectType.Table => GetIconGeometry("table"),
            TomObjectType.Column or TomObjectType.DataColumn => GetIconGeometry("column"),
            TomObjectType.CalculatedColumn or TomObjectType.CalculatedTableColumn => GetIconGeometry("calccolumn"),
            TomObjectType.Measure => GetIconGeometry("measure"),
            TomObjectType.Hierarchy => GetIconGeometry("hierarchy"),
            TomObjectType.HierarchyLevel => GetIconGeometry("level"),
            TomObjectType.Partition => GetIconGeometry("partition"),
            TomObjectType.Relationship => GetIconGeometry("relationship"),
            TomObjectType.Perspective => GetIconGeometry("perspective"),
            TomObjectType.Culture => GetIconGeometry("culture"),
            TomObjectType.Role => GetIconGeometry("role"),
            TomObjectType.CalculationGroup => GetIconGeometry("calcgroup"),
            TomObjectType.CalculationItem => GetIconGeometry("calcitem"),
            TomObjectType.SharedExpression => GetIconGeometry("expression"),
            _ => GetIconGeometry("folder")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string GetIconGeometry(string type)
    {
        return type switch
        {
            "model" => "M2,2 L14,2 L14,14 L2,14 Z M4,6 L12,6 M4,10 L12,10",
            "folder" => "M1,3 L6,3 L7,1 L14,1 L14,13 L1,13 Z",
            "datasource" => "M3,2 C3,1 13,1 13,2 L13,13 C13,14 3,14 3,13 Z M3,2 C3,3 13,3 13,2",
            "table" => "M2,2 L14,2 L14,14 L2,14 Z M2,6 L14,6 M2,10 L14,10 M7,2 L7,14",
            "column" => "M6,1 L10,1 L10,15 L6,15 Z",
            "calccolumn" => "M6,1 L10,1 L10,15 L6,15 Z M4,8 L12,8 M8,5 L8,11",
            "measure" => "M3,3 L8,1 L13,3 L13,13 L8,15 L3,13 Z",
            "hierarchy" => "M8,1 L8,5 M4,5 L12,5 M4,5 L4,9 M12,5 L12,9 M4,9 L4,13 M12,9 L12,13",
            "level" => "M4,8 L12,8 M4,4 L4,12",
            "partition" => "M2,2 L14,2 L14,14 L2,14 Z M8,2 L8,14",
            "relationship" => "M2,8 L6,4 L6,12 Z M14,8 L10,4 L10,12 Z M6,8 L10,8",
            "perspective" => "M8,2 C12,2 14,8 14,8 C14,8 12,14 8,14 C4,14 2,8 2,8 C2,8 4,2 8,2 Z",
            "culture" => "M8,1 C11.8,1 15,4.7 15,8 C15,11.8 11.8,15 8,15 C4.2,15 1,11.8 1,8 C1,4.7 4.2,1 8,1 Z M1,8 L15,8",
            "role" => "M8,2 C9.6,2 11,3.6 11,5 C11,6.7 9.6,8 8,8 C6.4,8 5,6.7 5,5 C5,3.6 6.4,2 8,2 Z M3,14 C3,11 5,9 8,9 C11,9 13,11 13,14",
            "calcgroup" => "M2,2 L14,2 L14,14 L2,14 Z M5,8 L11,8 M8,5 L8,11",
            "calcitem" => "M5,8 L11,8 M8,5 L8,11",
            "expression" => "M5,2 L3,8 L5,14 M11,2 L13,8 L11,14",
            _ => "M1,3 L6,3 L7,1 L14,1 L14,13 L1,13 Z"
        };
    }
}

/// <summary>
/// Converts a TomObjectType to the appropriate icon brush color.
/// </summary>
public class ObjectTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TomObjectType objType)
            return Application.Current.FindResource("FolderIconColor") as SolidColorBrush ?? Brushes.Gray;

        string key = objType switch
        {
            TomObjectType.Model => "ModelIconColor",
            TomObjectType.Table => "TableIconColor",
            TomObjectType.Column or TomObjectType.DataColumn => "ColumnIconColor",
            TomObjectType.CalculatedColumn or TomObjectType.CalculatedTableColumn => "CalculatedColumnIconColor",
            TomObjectType.Measure => "MeasureIconColor",
            TomObjectType.Hierarchy or TomObjectType.HierarchyLevel => "HierarchyIconColor",
            TomObjectType.Relationship => "RelationshipIconColor",
            TomObjectType.Partition => "PartitionIconColor",
            TomObjectType.Role => "RoleIconColor",
            TomObjectType.Perspective => "PerspectiveIconColor",
            _ => "FolderIconColor"
        };

        return Application.Current.TryFindResource(key) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
