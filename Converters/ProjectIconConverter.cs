using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Modrix.Converters
{
    public class ProjectIconConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not string location || values[1] is not string modId)
            {
                return "pack://application:,,,/Resources/ModrixIcon.ico";
            }

            var iconPath = Path.Combine(location, "src", "main", "resources", "assets", modId, "icon.png");
            if (File.Exists(iconPath))
            {
                return iconPath;
            }

            return "pack://application:,,,/Resources/ModrixIcon.ico";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}