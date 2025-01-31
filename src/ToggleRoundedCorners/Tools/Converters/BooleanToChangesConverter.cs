namespace Dawn.Apps.ToggleRoundedCorners.Tools.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

internal class BooleanToChangesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue) return "no";

        return boolValue ? "yes" : "no";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}