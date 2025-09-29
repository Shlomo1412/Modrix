using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ModrixInstaller.Helpers;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool invert = parameter?.ToString() == "Inverted";
            
            if (invert)
                boolValue = !boolValue;
                
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool result = visibility == Visibility.Visible;
            bool invert = parameter?.ToString() == "Inverted";
            
            if (invert)
                result = !result;
                
            return result;
        }
        
        return false;
    }
}