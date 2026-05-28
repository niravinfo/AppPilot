using System;
using System.Globalization;
using System.Windows.Data;

namespace AppPilot.Converters;

/// <summary>
/// Converts a string to its first character (initial) in uppercase.
/// </summary>
public class InitialConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return str[0].ToString().ToUpperInvariant();
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
