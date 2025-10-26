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
    public partial class SetDetailsWindow : Window
    {
        private readonly string _setNumber;
        private readonly SetRepository _setRepository;
        private readonly InventoryRepository _inventoryRepository;
        private List<InventoryItem> _allInventory = new List<InventoryItem>();
        private string _setUrl;

        // Drag-to-scroll state
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _dragStartVerticalOffset;
        private const double DragThreshold = 5.0;

        public SetDetailsWindow(string setNumber)
        {
            InitializeComponent();
            _setNumber = setNumber;
            _setRepository = new SetRepository();
            _inventoryRepository = new InventoryRepository();

            LoadSetDetails();
        }

        private void LoadSetDetails()
        {
            // Load set info
            var set = _setRepository.GetSet(_setNumber);
            if (set != null)
            {
                SetImage.Source = set.SetImage;
                SetNameTextBlock.Text = set.SetName;
                SetNumberTextBlock.Text = set.SetNumber;
                _setUrl = set.SetURL;
                LocationTextBox.Text = set.SetLocation ?? "";
            }

            // Load inventory
            _allInventory = _inventoryRepository.GetSetInventory(_setNumber);
            PartCountTextBlock.Text = $"Total Parts: {_allInventory.Count}";
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? "";

            List<InventoryItem> filteredInventory;
            if (string.IsNullOrEmpty(searchText))
            {
                filteredInventory = _allInventory;
            }
            else
            {
                filteredInventory = _allInventory.Where(item =>
                    item.BrickDetail.PartName.ToLower().Contains(searchText) ||
                    item.PartNumber.ToLower().Contains(searchText) ||
                    item.BrickDetail.ColorName.ToLower().Contains(searchText)
                ).ToList();
            }

            InventoryDataGrid.ItemsSource = filteredInventory;
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

        // Search handlers
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();

            // Show/hide clear button
            if (!string.IsNullOrEmpty(SearchTextBox.Text))
            {
                ClearSearchButton.Visibility = Visibility.Visible;
            }
            else
            {
                ClearSearchButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            SearchTextBox.Focus();
        }

        private void SetName_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(_setUrl))
            {
                OpenUrl(_setUrl);
            }
        }

        private void SetNumber_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(_setUrl))
            {
                OpenUrl(_setUrl);
            }
        }

        private void LocationTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                string newLocation = LocationTextBox.Text.Trim();
                _setRepository.UpdateSetLocation(_setNumber, string.IsNullOrEmpty(newLocation) ? null : newLocation);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error updating location: {ex.Message}",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Drag-to-scroll handlers
        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                _dragStartPoint = e.GetPosition(scrollViewer);
                _dragStartVerticalOffset = scrollViewer.VerticalOffset;
                _isDragging = false; // Don't set to true yet - wait for movement threshold
                // Don't capture mouse yet - only capture when we start dragging
            }
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(scrollViewer);
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                // Check if we've moved past the threshold
                if (!_isDragging && Math.Abs(deltaY) > DragThreshold)
                {
                    _isDragging = true;
                    scrollViewer.CaptureMouse();
                    scrollViewer.Cursor = Cursors.ScrollNS;
                }

                // Only scroll if we're actually dragging
                if (_isDragging)
                {
                    double newOffset = _dragStartVerticalOffset - deltaY;
                    scrollViewer.ScrollToVerticalOffset(newOffset);
                }
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && scrollViewer.IsMouseCaptured)
            {
                scrollViewer.ReleaseMouseCapture();
                scrollViewer.Cursor = Cursors.Arrow;

                // If we were dragging, mark the event as handled to prevent click-through
                if (_isDragging)
                {
                    e.Handled = true;
                }

                _isDragging = false;
            }
        }
    }
}
