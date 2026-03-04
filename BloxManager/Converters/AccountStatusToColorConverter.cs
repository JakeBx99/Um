using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BloxManager.Models;

namespace BloxManager.Converters
{
    public class AccountStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AccountStatus status)
            {
                return status switch
                {
                    AccountStatus.Online => new SolidColorBrush(Color.FromRgb(33, 150, 243)),    // Blue
                    AccountStatus.InGame => new SolidColorBrush(Color.FromRgb(76, 175, 80)),    // Green  
                    AccountStatus.Offline => new SolidColorBrush(Color.FromRgb(220, 53, 69)),    // Red
                    AccountStatus.Expired => new SolidColorBrush(Color.FromRgb(255, 193, 7)),    // Amber/Orange
                    AccountStatus.Unknown => new SolidColorBrush(Color.FromRgb(108, 117, 125)), // Gray
                    _ => new SolidColorBrush(Color.FromRgb(108, 117, 125)), // Default gray
                };
            }
            return new SolidColorBrush(Color.FromRgb(108, 117, 125)); // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
