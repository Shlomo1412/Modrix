using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace ModrixInstaller.Helpers;

public class StringToSymbolIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string iconName && !string.IsNullOrEmpty(iconName))
        {
            // Parse the string to the appropriate SymbolRegular enum value
            if (Enum.TryParse<SymbolRegular>(iconName, out var symbol))
            {
                return symbol;
            }
        }
        
        // Default fallback icon
        return SymbolRegular.ArrowRight24;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}