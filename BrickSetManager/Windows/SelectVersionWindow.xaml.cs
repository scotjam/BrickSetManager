using System.Windows;
using System.Windows.Controls;

namespace BrickSetManager.Windows
{
    public partial class SelectVersionWindow : Window
    {
        public int SelectedVersion { get; private set; }

        public SelectVersionWindow(string setNumber)
        {
            InitializeComponent();
            SetNumberRun.Text = setNumber;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (VersionComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                SelectedVersion = int.Parse(selectedItem.Tag.ToString());
            }
            else
            {
                SelectedVersion = 2; // Default to Latest Version
            }

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
