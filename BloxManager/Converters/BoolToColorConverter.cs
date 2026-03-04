using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BloxManager.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isValid)
            {
                return isValid ? new SolidColorBrush(Color.FromRgb(76, 175, 80))    // Green - online/valid
                               : new SolidColorBrush(Color.FromRgb(220, 53, 69));    // Red - offline/invalid
            }
            return new SolidColorBrush(Color.FromRgb(108, 117, 125)); // Gray - unknown
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
