using System.Windows;
using System.Windows.Input;

namespace BloxManager.Views
{
    public partial class PromptWindow : Window
    {
        public string InputText => InputTextBox.Text;

        public PromptWindow(string title, string message, string defaultText = "")
        {
            InitializeComponent();
            Title = title;
            MessageTextBlock.Text = message;
            InputTextBox.Text = defaultText;

            Loaded += (s, e) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
