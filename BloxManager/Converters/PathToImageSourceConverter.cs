using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace BloxManager.Converters
{
    public class PathToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null!;
            var raw = value.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return null!;

            var path = raw.Trim().Trim('"');
            path = Environment.ExpandEnvironmentVariables(path);
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
            }
            if (!File.Exists(path)) return null!;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                if (bitmap.CanFreeze) bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null!;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
