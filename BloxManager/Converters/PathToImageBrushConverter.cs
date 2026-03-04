using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BloxManager.Converters
{
    public class PathToImageBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 1) return DependencyProperty.UnsetValue;

            string imagePath = values[0] as string;
            string stretchStr = values.Length > 1 ? values[1] as string : "UniformToFill";
            string alignmentStr = values.Length > 2 ? values[2] as string : "Center";
            double opacity = values.Length > 3 ? (values[3] is double d ? d : 1.0) : 1.0;

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                    if (bitmap.CanFreeze) bitmap.Freeze();

                    Stretch stretch = Stretch.UniformToFill;
                    if (!string.IsNullOrEmpty(stretchStr))
                    {
                        Enum.TryParse(stretchStr, out stretch);
                    }

                    AlignmentX alignmentX = AlignmentX.Center;
                    AlignmentY alignmentY = AlignmentY.Center;

                    if (!string.IsNullOrEmpty(alignmentStr))
                    {
                        switch (alignmentStr)
                        {
                            case "Left": alignmentX = AlignmentX.Left; break;
                            case "Right": alignmentX = AlignmentX.Right; break;
                            case "Top": alignmentY = AlignmentY.Top; break;
                            case "Bottom": alignmentY = AlignmentY.Bottom; break;
                            case "TopLeft": alignmentX = AlignmentX.Left; alignmentY = AlignmentY.Top; break;
                            case "TopRight": alignmentX = AlignmentX.Right; alignmentY = AlignmentY.Top; break;
                            case "BottomLeft": alignmentX = AlignmentX.Left; alignmentY = AlignmentY.Bottom; break;
                            case "BottomRight": alignmentX = AlignmentX.Right; alignmentY = AlignmentY.Bottom; break;
                        }
                    }

                    var brush = new ImageBrush(bitmap)
                    {
                        Stretch = stretch,
                        AlignmentX = alignmentX,
                        AlignmentY = alignmentY,
                        Opacity = opacity
                    };
                    if (brush.CanFreeze) brush.Freeze();
                    return brush;
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing, Binding.DoNothing, Binding.DoNothing, Binding.DoNothing };
        }
    }
}
