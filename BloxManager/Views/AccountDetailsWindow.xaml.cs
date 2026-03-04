using BloxManager.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BloxManager.Views
{
    public partial class AccountDetailsWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public AccountDetailsWindow(AccountViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            SourceInitialized += AccountDetailsWindow_SourceInitialized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AccountDetailsWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
    }
}
