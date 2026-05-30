using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AppPilot.Converters;

/// <summary>
/// Converts work area height to a max height that respects small screen sizes.
/// Returns 90% of work area height to ensure dialogs fit on screen.
/// </summary>
public class WorkAreaHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Rect workArea)
        {
            return workArea.Height * 0.9;
        }
        if (value is double height)
        {
            return height * 0.9;
        }
        return 600; // Default fallback
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
