using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AppPilot.Converters;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                hex = hex.Trim();
                if (hex.StartsWith("#"))
                {
                    var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex);
                    return brush;
                }
            }
            catch { }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
        }
        return string.Empty;
    }
}
