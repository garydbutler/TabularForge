using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TabularForge.UI.Converters;

/// <summary>
/// Converts a shortcut conflict state to a color brush.
/// </summary>
public class ConflictToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool hasConflict && hasConflict)
            return new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47)); // Error red
        return new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)); // Normal text
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts bool IsCustomized to font weight (bold if customized).
/// </summary>
public class CustomizedToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCustomized && isCustomized)
            return FontWeights.Bold;
        return FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts IsModified translation flag to background color.
/// </summary>
public class ModifiedToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isModified && isModified)
            return new SolidColorBrush(Color.FromArgb(0x30, 0x4E, 0xC9, 0xB0)); // Light green tint
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts layout preset IsBuiltIn to icon string.
/// </summary>
public class BuiltInToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBuiltIn && isBuiltIn)
            return "P"; // Pin icon
        return "U"; // User icon
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
