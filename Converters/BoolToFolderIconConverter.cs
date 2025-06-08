using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Modrix.Converters
{
    public class BoolToFolderIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isDirectory && isDirectory
                ? SymbolRegular.Folder20
                : SymbolRegular.Document20;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}