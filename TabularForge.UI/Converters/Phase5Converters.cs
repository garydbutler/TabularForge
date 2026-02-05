using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TabularForge.Core.Models;

namespace TabularForge.UI.Converters;

/// <summary>
/// Converts BpaSeverity to a color brush.
/// </summary>
public class BpaSeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BpaSeverity severity)
        {
            return severity switch
            {
                BpaSeverity.Error => new SolidColorBrush(Color.FromRgb(0xE8, 0x40, 0x40)),
                BpaSeverity.Warning => new SolidColorBrush(Color.FromRgb(0xD4, 0xA0, 0x17)),
                BpaSeverity.Info => new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts BpaSeverity to an icon character.
/// </summary>
public class BpaSeverityToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BpaSeverity severity)
        {
            return severity switch
            {
                BpaSeverity.Error => "X",
                BpaSeverity.Warning => "!",
                BpaSeverity.Info => "i",
                _ => "?"
            };
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts ScriptExecutionState to color.
/// </summary>
public class ExecutionStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScriptExecutionState state)
        {
            return state switch
            {
                ScriptExecutionState.Ready => new SolidColorBrush(Colors.Gray),
                ScriptExecutionState.Running => new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x4E)),
                ScriptExecutionState.Completed => new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9)),
                ScriptExecutionState.Failed => new SolidColorBrush(Color.FromRgb(0xE8, 0x40, 0x40)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts ImportWizardStep (enum int) to step number string (1-based).
/// </summary>
public class StepNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ImportWizardStep step)
            return ((int)step + 1).ToString();
        if (value is int intVal)
            return (intVal + 1).ToString();
        return "1";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts ScriptDiagnosticSeverity to color.
/// </summary>
public class ScriptDiagSeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScriptDiagnosticSeverity severity)
        {
            return severity switch
            {
                ScriptDiagnosticSeverity.Error => new SolidColorBrush(Color.FromRgb(0xE8, 0x40, 0x40)),
                ScriptDiagnosticSeverity.Warning => new SolidColorBrush(Color.FromRgb(0xD4, 0xA0, 0x17)),
                ScriptDiagnosticSeverity.Info => new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts ImportSourceType to visibility for connection config sections.
/// </summary>
public class SourceTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ImportSourceType sourceType && parameter is string paramStr)
        {
            if (Enum.TryParse<ImportSourceType>(paramStr, out var target))
                return sourceType == target ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
