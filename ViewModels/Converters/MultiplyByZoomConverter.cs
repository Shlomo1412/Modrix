using System;
using System.Globalization;
using System.Windows.Data;

namespace Modrix.ViewModels.Converters;

public class MultiplyByZoomConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int size && parameter is int zoom)
        {
            return size * zoom;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}