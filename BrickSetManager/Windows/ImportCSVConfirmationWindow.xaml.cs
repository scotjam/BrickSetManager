using System.Windows;

namespace BrickSetManager.Windows
{
    public partial class ImportCSVConfirmationWindow : Window
    {
        public bool RemoveNotInCSV { get; private set; }

        public ImportCSVConfirmationWindow()
        {
            InitializeComponent();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveNotInCSV = RemoveNotInCSVCheckBox.IsChecked == true;
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
