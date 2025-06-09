using System;
using System.Globalization;
using System.Windows.Data;
using Modrix.ViewModels.Pages;
using Wpf.Ui.Appearance;

namespace Modrix.ViewModels.Converters;

public class ToolToAppearanceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is EditorTool currentTool && parameter is string toolStr &&
            Enum.TryParse<EditorTool>(toolStr, out var paramTool))
        {
            return currentTool == paramTool ? "Primary" : "Secondary";
        }
        return "Secondary";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}