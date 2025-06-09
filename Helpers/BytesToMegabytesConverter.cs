using System;
using System.Globalization;
using System.Windows.Data;

namespace Modrix.Helpers
{
    public class BytesToMegabytesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                return bytes / (1024 * 1024);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double megabytes)
            {
                return (long)(megabytes * 1024 * 1024);
            }
            return 0;
        }
    }
}