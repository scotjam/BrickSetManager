using System.Windows;
using BrickSetManager.Database;

namespace BrickSetManager.Windows
{
    public partial class BrickSetsWindow : Window
    {
        private readonly InventoryRepository _inventoryRepository;

        public BrickSetsWindow(string partNumber, int colorID, string partName)
        {
            InitializeComponent();
            _inventoryRepository = new InventoryRepository();

            BrickNameTextBlock.Text = partName;
            BrickNumberTextBlock.Text = $"Part #{partNumber} (Color ID: {colorID})";

            LoadSets(partNumber, colorID);
        }

        private void LoadSets(string partNumber, int colorID)
        {
            var sets = _inventoryRepository.GetSetsContainingBrick(partNumber, colorID);
            SetsDataGrid.ItemsSource = sets;
        }
    }
}
