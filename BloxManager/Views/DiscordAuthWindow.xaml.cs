using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using BloxManager.ViewModels;

namespace BloxManager.Views
{
    public partial class DiscordAuthWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public DiscordAuthWindow(DiscordAuthViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            SourceInitialized += DiscordAuthWindow_SourceInitialized;
        }

        private void DiscordAuthWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
