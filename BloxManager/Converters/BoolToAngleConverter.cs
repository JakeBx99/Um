using System;
using System.Globalization;
using System.Windows.Data;

namespace BloxManager.Converters
{
    public class BoolToAngleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Debug.WriteLine($"BoolToAngleConverter.Convert called with value: {value}, value type: {value?.GetType()}");
            
            if (value is bool isExpanded)
            {
                var angle = isExpanded ? 0 : 180;
                System.Diagnostics.Debug.WriteLine($"Returning angle: {angle} for isExpanded: {isExpanded}");
                return angle;
            }
            
            System.Diagnostics.Debug.WriteLine("Returning default angle: 180");
            return 180; // Default to collapsed state
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
