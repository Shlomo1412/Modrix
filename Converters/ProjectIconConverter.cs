using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Modrix.Converters
{
    public class ProjectIconConverter : IMultiValueConverter
    {
        private static readonly Uri defaultIcon = new("pack://application:,,,/Resources/ModrixIcon.ico");

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 2 || values[0] is not string location || values[1] is not string modId)
                {
                    return new BitmapImage(defaultIcon);
                }

                var iconPath = Path.Combine(location, "src", "main", "resources", "assets", modId, "icon.png");
                if (File.Exists(iconPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(iconPath);
                    bitmap.EndInit();
                    bitmap.Freeze(); // Make it thread-safe
                    return bitmap;
                }

                return new BitmapImage(defaultIcon);
            }
            catch
            {
                // If anything goes wrong (file access, invalid image, etc.), return the default icon
                return new BitmapImage(defaultIcon);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}