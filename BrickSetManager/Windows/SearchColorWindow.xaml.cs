using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BrickSetManager.Database;
using BrickSetManager.Models;

namespace BrickSetManager.Windows
{
    public partial class SearchColorWindow : Window
    {
        private readonly BrickRepository _brickRepository;
        private readonly InventoryRepository _inventoryRepository;
        private const string AnyColorOption = "Any color";

        public SearchColorWindow()
        {
            InitializeComponent();
            _brickRepository = new BrickRepository();
            _inventoryRepository = new InventoryRepository();

            LoadColors();
        }

        private void LoadColors()
        {
            var colors = _brickRepository.GetAllColors();

            // Add "Any color" at the top
            var colorList = new List<string> { AnyColorOption };
            colorList.AddRange(colors);

            ColorComboBox.ItemsSource = colorList;
            ColorComboBox.SelectedIndex = 0; // Select "Any color" by default

            if (!colors.Any())
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                BricksDataGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch();
            }
        }

        private void PerformSearch()
        {
            string selectedColor = ColorComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedColor))
            {
                ResultCountTextBlock.Text = "Please select a color";
                return;
            }

            // Get all bricks based on color selection
            List<BrickDetail> allBricks;
            if (selectedColor == AnyColorOption)
            {
                allBricks = _inventoryRepository.GetAllBricksWithQuantity();
            }
            else
            {
                allBricks = _brickRepository.GetBricksByColor(selectedColor);
            }

            // Apply text filter
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? "";
            List<BrickDetail> filteredBricks;

            if (string.IsNullOrEmpty(searchText))
            {
                filteredBricks = allBricks;
            }
            else
            {
                // Split search text into individual words
                var searchWords = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                filteredBricks = allBricks.Where(b =>
                {
                    string partNameLower = b.PartName.ToLower();
                    string partNumberLower = b.PartNumber.ToLower();
                    string colorNameLower = b.ColorName.ToLower();

                    // Check if all search words appear in any of the fields
                    return searchWords.All(word =>
                        partNameLower.Contains(word) ||
                        partNumberLower.Contains(word) ||
                        colorNameLower.Contains(word)
                    );
                }).ToList();
            }

            // Update UI
            if (filteredBricks.Any())
            {
                BricksDataGrid.ItemsSource = filteredBricks;
                BricksDataGrid.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;

                // Update result count
                string colorText = selectedColor == AnyColorOption ? "all colors" : selectedColor;
                ResultCountTextBlock.Text = $"Found {filteredBricks.Count} brick(s) in {colorText}";
            }
            else
            {
                BricksDataGrid.ItemsSource = null;
                BricksDataGrid.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                ResultCountTextBlock.Text = "No bricks found";
            }

            // Show/hide clear button
            ClearSearchButton.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BricksDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BricksDataGrid.SelectedItem is BrickDetail brick)
            {
                var brickSetsWindow = new BrickSetsWindow(brick.PartNumber, brick.ColorID, brick.PartName);
                brickSetsWindow.Owner = this;
                brickSetsWindow.ShowDialog();
            }
        }

        private void ViewPartButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement button && button.Tag is string url)
            {
                OpenUrl(url);
            }
        }

        private void PriceGuideButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement button && button.Tag is string url)
            {
                OpenUrl(url);
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Could not open URL", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            ClearSearchButton.Visibility = Visibility.Collapsed;
            SearchTextBox.Focus();
        }
    }
}
