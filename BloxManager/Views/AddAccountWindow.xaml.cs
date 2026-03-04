using BloxManager.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BloxManager.Views
{
    public partial class AddAccountWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public AddAccountWindow(AddAccountViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            SourceInitialized += AddAccountWindow_SourceInitialized;

            if (viewModel != null)
            {
                viewModel.RequestClose += result =>
                {
                    DialogResult = result == true;
                    Close();
                };
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void AddAccountWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
    }
}
