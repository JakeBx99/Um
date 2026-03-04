using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace BloxManager.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(Global_PreviewMouseWheel), true);
            AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Global_PreviewMouseLeftButtonDown), true);

            try
            {
                var uri = new Uri("pack://application:,,,/Assets/BloxManager_Logo.ico");
                var resource = Application.GetResourceStream(uri);
                if (resource != null)
                {
                    using var icon = new Icon(resource.Stream);
                    var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    LogoImageSidebar.Source = source;
                    LogoImageHeader.Source = source;
                }
            }
            catch
            {
            }
        }
 
        private void StretchCombo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        private void StretchCombo_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is ComboBox cb)
                {
                    cb.IsDropDownOpen = true;
                    e.Handled = true;
                    return;
                }
                source = VisualTreeHelper.GetParent(source);
            }
        }

        private void Global_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is ComboBox)
                {
                    e.Handled = true;
                    return;
                }
                source = VisualTreeHelper.GetParent(source);
            }
        }

        private void Global_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is ComboBox cb)
                {
                    cb.IsDropDownOpen = true;
                    e.Handled = true;
                    return;
                }
                source = VisualTreeHelper.GetParent(source);
            }
        }
 
    }
}
