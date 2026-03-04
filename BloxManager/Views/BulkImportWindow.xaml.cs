using System.Windows;

namespace BloxManager.Views
{
    public partial class BulkImportWindow : Window
    {
        public BulkImportWindow()
        {
            InitializeComponent();
            DataContext = BloxManager.App.GetService<ViewModels.BulkImportViewModel>();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
