using System;
using System.Windows;

namespace BloxManager.Views
{
    public partial class ChromiumDownloadWindow : Window
    {
        public ChromiumDownloadWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        public void UpdateProgress(double percent, string message)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = percent;
            StatusText.Text = message;
        }

        public void MarkCompleted()
        {
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 100;
            StatusText.Text = "Chromium download complete.";
        }
    }
}

