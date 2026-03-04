using System;
using System.Globalization;
using System.Windows.Data;

namespace BloxManager.Converters
{
    public class UsernameToInitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string username && !string.IsNullOrEmpty(username))
            {
                return username.Substring(0, Math.Min(2, username.Length)).ToUpper();
            }
            return "??";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
