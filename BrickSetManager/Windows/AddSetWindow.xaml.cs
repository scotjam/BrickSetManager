using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace BrickSetManager.Windows
{
    public partial class AddSetWindow : Window
    {
        public string SetNumber { get; private set; }

        public AddSetWindow()
        {
            InitializeComponent();
            SetNumberTextBox.Focus();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddSetToQueue();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SetNumberTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddSetToQueue();
                e.Handled = true;
            }
        }

        private void AddSetToQueue()
        {
            string setNumber = SetNumberTextBox.Text.Trim();

            if (string.IsNullOrEmpty(setNumber))
            {
                MessageBox.Show("Please enter a set number.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Automatically add "-1" if the set number doesn't end with a dash and digit(s)
            // Pattern matches: ends with "-" followed by one or more digits
            if (!Regex.IsMatch(setNumber, @"-\d+$"))
            {
                setNumber += "-1";
            }

            SetNumber = setNumber;
            DialogResult = true;

            // Clear the textbox so user can add another set
            SetNumberTextBox.Clear();
            SetNumberTextBox.Focus();
        }
    }
}
