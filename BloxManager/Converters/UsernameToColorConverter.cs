using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BloxManager.Converters
{
    public class UsernameToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string username && !string.IsNullOrEmpty(username))
            {
                var hues = new[] { 200, 260, 320, 40, 160 };
                var hue = hues[username[0] % hues.Length];
                var r = (byte)((hue * 3 + 0) % 3);
                var g = (byte)((hue * 3 + 1) % 3);
                var b = (byte)((hue * 3 + 2) % 3);
                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            return new SolidColorBrush(Color.FromRgb(128, 128, 128));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
