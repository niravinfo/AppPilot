using System;
using System.Globalization;
using System.Windows.Data;

namespace AppPilot.Converters;

public class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && parameter is string param)
        {
            return str == param;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && parameter is string param && isChecked)
        {
            return param;
        }

        return Binding.DoNothing;
    }
}
