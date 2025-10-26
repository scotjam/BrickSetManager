using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BrickSetManager.Database;
using BrickSetManager.Models;
using BrickSetManager.Services;
using BrickSetManager.Windows;
using Microsoft.Win32;

namespace BrickSetManager
{
    public partial class MainWindow : Window
    {
        private readonly SetRepository _setRepository;
        private readonly InventoryRepository _inventoryRepository;
        private readonly BrickRepository _brickRepository;
        private readonly SetQueueProcessor _queueProcessor;

        // View toggle state
        private bool _isListView = false;

        // Drag-to-scroll state
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _dragStartVerticalOffset;
        private const double DragThreshold = 5.0; // Minimum pixels to move before starting drag

        // Search/filter state
        private List<BrickSet> _allSets = new List<BrickSet>();

        public MainWindow()
        {
            InitializeComponent();
            _setRepository = new SetRepository();
            _inventoryRepository = new InventoryRepository();
            _brickRepository = new BrickRepository();
            _queueProcessor = new SetQueueProcessor();

            // Subscribe to queue processor events
            _queueProcessor.SetProcessingStarted += OnSetProcessingStarted;
            _queueProcessor.SetProcessingCompleted += OnSetProcessingCompleted;
            _queueProcessor.SetProcessingFailed += OnSetProcessingFailed;
            _queueProcessor.QueueEmptied += OnQueueEmptied;

            // Set the brick icon for the window
            SetBrickIcon();

            LoadSets();
        }

        private void LoadSets()
        {
            _allSets = _setRepository.GetAllSets();
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string searchText = SearchTextBox?.Text?.Trim().ToLower() ?? "";

            List<BrickSet> filteredSets;
            if (string.IsNullOrEmpty(searchText))
            {
                filteredSets = _allSets;
            }
            else
            {
                // Split search text into individual words
                var searchWords = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                filteredSets = _allSets.Where(s =>
                {
                    string setNameLower = s.SetName.ToLower();
                    string setNumberLower = s.SetNumber.ToLower();

                    // Check if all search words appear in either the set name or set number
                    return searchWords.All(word =>
                        setNameLower.Contains(word) || setNumberLower.Contains(word)
                    );
                }).ToList();
            }

            if (filteredSets.Any())
            {
                // Populate both gallery and list views
                SetsItemsControl.ItemsSource = filteredSets;
                ListDataGrid.ItemsSource = filteredSets;
                EmptyStatePanel.Visibility = Visibility.Collapsed;

                // Calculate and display total count
                int uniqueSets = filteredSets.Count;
                int totalSets = filteredSets.Sum(s => s.Quantity);
                TotalSetsTextBlock.Text = uniqueSets == totalSets
                    ? $"Total: {totalSets} set{(totalSets == 1 ? "" : "s")}"
                    : $"Total: {totalSets} set{(totalSets == 1 ? "" : "s")} ({uniqueSets} unique)";
            }
            else
            {
                SetsItemsControl.ItemsSource = null;
                ListDataGrid.ItemsSource = null;
                EmptyStatePanel.Visibility = Visibility.Visible;
                TotalSetsTextBlock.Text = "Total: 0 sets";
            }
        }

        private async void AddSetButton_Click(object sender, RoutedEventArgs e)
        {
            var addSetWindow = new AddSetWindow();
            addSetWindow.Owner = this;
            bool? result = addSetWindow.ShowDialog();

            if (result == true && !string.IsNullOrEmpty(addSetWindow.SetNumber))
            {
                string setNumber = addSetWindow.SetNumber;
                int inventoryVersion = 1; // Default to Old/Standard

                // Check if multiple versions exist
                var scraper = new Services.Scraper();
                bool hasMultipleVersions = await scraper.HasMultipleVersionsAsync(setNumber);

                if (hasMultipleVersions)
                {
                    // Show version selection window
                    var selectVersionWindow = new SelectVersionWindow(setNumber);
                    selectVersionWindow.Owner = this;

                    if (selectVersionWindow.ShowDialog() == true)
                    {
                        inventoryVersion = selectVersionWindow.SelectedVersion;
                    }
                    else
                    {
                        // User cancelled version selection, go back to add set window
                        AddSetButton_Click(sender, e);
                        return;
                    }
                }

                // Add set to queue with inventory version
                _queueProcessor.EnqueueSet(setNumber, 1, inventoryVersion);

                // Show progress panel
                UpdateProgressUI();

                // Keep the dialog open so user can add more sets
                AddSetButton_Click(sender, e);
            }
        }

        private void SearchColorButton_Click(object sender, RoutedEventArgs e)
        {
            var searchColorWindow = new SearchColorWindow();
            searchColorWindow.Owner = this;
            searchColorWindow.ShowDialog();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSets();
        }

        private void SetCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BrickSet set)
            {
                var setDetailsWindow = new SetDetailsWindow(set.SetNumber);
                setDetailsWindow.Owner = this;
                setDetailsWindow.ShowDialog();
            }
        }

        private void AddCopy_Click(object sender, RoutedEventArgs e)
        {
            BrickSet set = null;
            if (sender is MenuItem menuItem)
            {
                // Try to get set from menu item's DataContext (gallery view)
                set = menuItem.DataContext as BrickSet;

                // If not found, try to get from DataGrid's selected item (list view)
                if (set == null && ListDataGrid.SelectedItem is BrickSet selectedSet)
                {
                    set = selectedSet;
                }
            }

            if (set != null)
            {
                try
                {
                    set.Quantity++;
                    _setRepository.UpdateQuantity(set.SetNumber, set.Quantity);
                    LoadSets(); // Refresh to show updated quantity badge
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error updating quantity: {ex.Message}",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void RemoveCopy_Click(object sender, RoutedEventArgs e)
        {
            BrickSet set = null;
            if (sender is MenuItem menuItem)
            {
                // Try to get set from menu item's DataContext (gallery view)
                set = menuItem.DataContext as BrickSet;

                // If not found, try to get from DataGrid's selected item (list view)
                if (set == null && ListDataGrid.SelectedItem is BrickSet selectedSet)
                {
                    set = selectedSet;
                }
            }

            if (set != null)
            {
                try
                {
                    if (set.Quantity > 1)
                    {
                        set.Quantity--;
                        _setRepository.UpdateQuantity(set.SetNumber, set.Quantity);
                        LoadSets(); // Refresh to show updated quantity badge
                    }
                    else
                    {
                        // If quantity is 1, ask if they want to delete the set
                        var result = MessageBox.Show(
                            $"This is the last copy of '{set.SetName}'. Do you want to delete this set completely?",
                            "Delete Set",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            _inventoryRepository.DeleteSetInventory(set.SetNumber);
                            _setRepository.DeleteSet(set.SetNumber);
                            LoadSets();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error updating quantity: {ex.Message}",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void SetQuantity_Click(object sender, RoutedEventArgs e)
        {
            BrickSet set = null;
            if (sender is MenuItem menuItem)
            {
                // Try to get set from menu item's DataContext (gallery view)
                set = menuItem.DataContext as BrickSet;

                // If not found, try to get from DataGrid's selected item (list view)
                if (set == null && ListDataGrid.SelectedItem is BrickSet selectedSet)
                {
                    set = selectedSet;
                }
            }

            if (set != null)
            {
                try
                {
                    // Create a simple input dialog
                    var inputDialog = new Window
                    {
                        Title = "Set Quantity",
                        Width = 300,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        ResizeMode = ResizeMode.NoResize
                    };

                    var grid = new Grid { Margin = new Thickness(20) };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var label = new TextBlock
                    {
                        Text = $"Enter quantity for '{set.SetName}':",
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetRow(label, 0);

                    var textBox = new TextBox
                    {
                        Text = set.Quantity.ToString(),
                        MaxLength = 3
                    };
                    Grid.SetRow(textBox, 2);
                    textBox.SelectAll();
                    textBox.Focus();

                    var buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetRow(buttonPanel, 4);

                    var okButton = new Button
                    {
                        Content = "OK",
                        Width = 75,
                        Margin = new Thickness(0, 0, 10, 0),
                        IsDefault = true
                    };
                    okButton.Click += (s, args) => inputDialog.DialogResult = true;

                    var cancelButton = new Button
                    {
                        Content = "Cancel",
                        Width = 75,
                        IsCancel = true
                    };

                    buttonPanel.Children.Add(okButton);
                    buttonPanel.Children.Add(cancelButton);

                    grid.Children.Add(label);
                    grid.Children.Add(textBox);
                    grid.Children.Add(buttonPanel);
                    inputDialog.Content = grid;

                    if (inputDialog.ShowDialog() == true)
                    {
                        if (int.TryParse(textBox.Text, out int newQuantity) && newQuantity > 0)
                        {
                            _setRepository.UpdateQuantity(set.SetNumber, newQuantity);
                            LoadSets(); // Refresh to show updated quantity badge
                        }
                        else
                        {
                            MessageBox.Show(
                                "Please enter a valid quantity (must be greater than 0).",
                                "Invalid Quantity",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error updating quantity: {ex.Message}",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void SetLocation_Click(object sender, RoutedEventArgs e)
        {
            BrickSet set = null;
            if (sender is MenuItem menuItem)
            {
                set = menuItem.DataContext as BrickSet;
                if (set == null && ListDataGrid.SelectedItem is BrickSet selectedSet)
                {
                    set = selectedSet;
                }
            }

            if (set != null)
            {
                try
                {
                    // Create input dialog for location
                    var inputDialog = new Window
                    {
                        Title = "Set Location",
                        Width = 500,
                        Height = 180,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        ResizeMode = ResizeMode.NoResize
                    };

                    var grid = new Grid { Margin = new Thickness(20) };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var label = new TextBlock
                    {
                        Text = $"Enter location for '{set.SetName}':",
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetRow(label, 0);

                    var textBox = new TextBox
                    {
                        Text = set.SetLocation ?? "",
                        MaxLength = 100
                    };
                    Grid.SetRow(textBox, 2);
                    textBox.SelectAll();
                    textBox.Focus();

                    var buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Grid.SetRow(buttonPanel, 4);

                    var okButton = new Button
                    {
                        Content = "OK",
                        Width = 75,
                        Margin = new Thickness(0, 0, 10, 0),
                        IsDefault = true
                    };
                    okButton.Click += (s, args) => inputDialog.DialogResult = true;

                    var cancelButton = new Button
                    {
                        Content = "Cancel",
                        Width = 75,
                        IsCancel = true
                    };

                    buttonPanel.Children.Add(okButton);
                    buttonPanel.Children.Add(cancelButton);

                    grid.Children.Add(label);
                    grid.Children.Add(textBox);
                    grid.Children.Add(buttonPanel);
                    inputDialog.Content = grid;

                    if (inputDialog.ShowDialog() == true)
                    {
                        string newLocation = textBox.Text.Trim();
                        _setRepository.UpdateSetLocation(set.SetNumber, string.IsNullOrEmpty(newLocation) ? null : newLocation);
                        LoadSets(); // Refresh to show updated location
                    }
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
        }

        private void DeleteSet_Click(object sender, RoutedEventArgs e)
        {
            BrickSet set = null;
            if (sender is MenuItem menuItem)
            {
                // Try to get set from menu item's DataContext (gallery view)
                set = menuItem.DataContext as BrickSet;

                // If not found, try to get from DataGrid's selected item (list view)
                if (set == null && ListDataGrid.SelectedItem is BrickSet selectedSet)
                {
                    set = selectedSet;
                }
            }

            if (set != null)
            {
                // Show confirmation dialog
                var result = MessageBox.Show(
                    $"Are you sure you want to delete set '{set.SetName}' ({set.SetNumber})?\n\nThis will also delete all inventory items for this set.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Delete inventory items first (due to foreign key constraint)
                        _inventoryRepository.DeleteSetInventory(set.SetNumber);

                        // Delete the set
                        _setRepository.DeleteSet(set.SetNumber);

                        // Refresh the UI
                        LoadSets();

                        MessageBox.Show(
                            $"Set '{set.SetName}' has been deleted successfully.",
                            "Delete Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show(
                            $"Error deleting set: {ex.Message}",
                            "Delete Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        // Queue processor event handlers
        private void OnSetProcessingStarted(object sender, SetProcessingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressTextBlock.Text = $"Processing {e.SetNumber}: {e.Status}";
                UpdateProgressUI();
            });
        }

        private void OnSetProcessingCompleted(object sender, SetProcessingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressTextBlock.Text = $"Completed {e.SetNumber}: {e.SetName} - {e.Status}";
                LoadSets();
                UpdateProgressUI();
            });
        }

        private void OnSetProcessingFailed(object sender, SetProcessingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressTextBlock.Text = $"Failed {e.SetNumber}: {e.Status}";
                MessageBox.Show(
                    $"Failed to add set {e.SetNumber}:\n{e.Status}",
                    "Error Adding Set",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                UpdateProgressUI();
            });
        }

        private void OnQueueEmptied(object sender, System.EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
            });
        }

        private void UpdateProgressUI()
        {
            int queueCount = _queueProcessor.QueueCount;
            QueueCountTextBlock.Text = $"Queue: {queueCount}";

            if (_queueProcessor.IsProcessing || queueCount > 0)
            {
                ProgressPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Export Database",
                    Filter = "Database files (*.db)|*.db|All files (*.*)|*.*",
                    DefaultExt = "db",
                    FileName = $"BrickSets_Backup_{DateTime.Now:yyyy-MM-dd_HHmmss}.db"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Get the database path
                    string dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "BrickSetManager",
                        "BrickSets.db");

                    // Copy the database file
                    File.Copy(dbPath, saveFileDialog.FileName, overwrite: true);

                    MessageBox.Show(
                        $"Database exported successfully to:\n{saveFileDialog.FileName}",
                        "Export Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error exporting database: {ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ImportCSVButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog first
                var confirmWindow = new ImportCSVConfirmationWindow();
                confirmWindow.Owner = this;

                if (confirmWindow.ShowDialog() != true)
                {
                    return; // User cancelled
                }

                bool removeNotInCSV = confirmWindow.RemoveNotInCSV;

                var openFileDialog = new OpenFileDialog
                {
                    Title = "Import Rebrickable CSV",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Read and parse CSV file
                    var sets = new List<(string setNumber, int quantity, int inventoryVersion)>();
                    var lines = File.ReadAllLines(openFileDialog.FileName);

                    // Skip header row and parse set numbers with quantities and inventory version
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        // Split by comma - Format: Set Number, Quantity, Inventory Ver
                        var parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            string setNumber = parts[0].Trim();
                            int quantity = 1;
                            int inventoryVersion = 2; // Default to Latest Version

                            // Try to parse quantity from second column
                            if (int.TryParse(parts[1].Trim(), out int parsedQty) && parsedQty > 0)
                            {
                                quantity = parsedQty;
                            }

                            // Try to parse inventory version from third column if it exists
                            // 1 = vOld (Older Version), 2+ = vNew (Latest Version)
                            if (parts.Length >= 3 && int.TryParse(parts[2].Trim(), out int parsedVer))
                            {
                                inventoryVersion = parsedVer;
                            }

                            if (!string.IsNullOrEmpty(setNumber))
                            {
                                sets.Add((setNumber, quantity, inventoryVersion));
                            }
                        }
                    }

                    if (sets.Count > 0)
                    {
                        // Handle removal of sets not in CSV if requested
                        int removedCount = 0;
                        if (removeNotInCSV)
                        {
                            var csvSetNumbers = new HashSet<string>(sets.Select(s => s.setNumber));
                            var existingSets = _setRepository.GetAllSets();

                            foreach (var existingSet in existingSets)
                            {
                                if (!csvSetNumbers.Contains(existingSet.SetNumber))
                                {
                                    // Delete inventory items first (due to foreign key constraint)
                                    _inventoryRepository.DeleteSetInventory(existingSet.SetNumber);
                                    // Delete the set
                                    _setRepository.DeleteSet(existingSet.SetNumber);
                                    removedCount++;
                                }
                            }
                        }

                        // Get all existing sets to check what needs scraping
                        var existingSetsDict = _setRepository.GetAllSets()
                            .ToDictionary(s => s.SetNumber, s => s);

                        int queuedCount = 0;
                        int updatedCount = 0;

                        // Process each set from CSV
                        foreach (var (setNumber, quantity, inventoryVersion) in sets)
                        {
                            // Check if set exists with matching version
                            if (existingSetsDict.TryGetValue(setNumber, out var existingSet) &&
                                existingSet.InventoryVersion == inventoryVersion)
                            {
                                // Set exists with correct version - just update quantity without scraping
                                _setRepository.UpdateQuantity(setNumber, quantity);
                                updatedCount++;
                            }
                            else
                            {
                                // Set doesn't exist OR has different version - queue for scraping
                                _queueProcessor.EnqueueSet(setNumber, quantity, inventoryVersion);
                                queuedCount++;
                            }
                        }

                        // Show progress panel if anything was queued
                        if (queuedCount > 0)
                        {
                            UpdateProgressUI();
                        }

                        string message = "";
                        if (queuedCount > 0 && updatedCount > 0)
                        {
                            message = $"Added {queuedCount} sets to the processing queue.\nUpdated {updatedCount} existing sets (no scraping needed).";
                        }
                        else if (queuedCount > 0)
                        {
                            message = $"Added {queuedCount} sets to the processing queue.";
                        }
                        else if (updatedCount > 0)
                        {
                            message = $"Updated {updatedCount} existing sets (no scraping needed).";
                        }
                        if (removedCount > 0)
                        {
                            message += $"\n\nRemoved {removedCount} sets that were not in the CSV.";
                        }

                        MessageBox.Show(
                            message,
                            "CSV Import",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Refresh the UI to show removed sets or updated quantities
                        if (removedCount > 0 || updatedCount > 0)
                        {
                            LoadSets();
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "No valid set numbers found in the CSV file.",
                            "CSV Import",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error importing CSV: {ex.Message}",
                    "CSV Import Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Warn the user
                var confirmResult = MessageBox.Show(
                    "WARNING: Importing a database will replace all your current data!\n\n" +
                    "This cannot be undone. Make sure you have exported your current database first.\n\n" +
                    "Do you want to continue?",
                    "Confirm Import",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult != MessageBoxResult.Yes)
                    return;

                var openFileDialog = new OpenFileDialog
                {
                    Title = "Import Database",
                    Filter = "Database files (*.db)|*.db|All files (*.*)|*.*",
                    DefaultExt = "db"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Get the database path
                    string dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "BrickSetManager",
                        "BrickSets.db");

                    // Close any open connections by reinitializing
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Copy the selected file to replace the current database
                    File.Copy(openFileDialog.FileName, dbPath, overwrite: true);

                    MessageBox.Show(
                        "Database imported successfully!\n\nThe application will now restart to apply changes.",
                        "Import Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Restart the application
                    System.Diagnostics.Process.Start(
                        Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error importing database: {ex.Message}",
                    "Import Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClearDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // First confirmation - warn about the danger
                var firstConfirm = MessageBox.Show(
                    "⚠️ WARNING: This will permanently delete ALL sets and their inventory from the database!\n\n" +
                    "This action CANNOT be undone!\n\n" +
                    "Are you absolutely sure you want to clear the entire database?",
                    "Clear Database - First Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (firstConfirm != MessageBoxResult.Yes)
                    return;

                // Second confirmation - make them really think about it
                var secondConfirm = MessageBox.Show(
                    "⚠️ FINAL WARNING ⚠️\n\n" +
                    "You are about to delete ALL data from your database:\n" +
                    $"• {_allSets.Count} sets will be deleted\n" +
                    "• All inventory items will be deleted\n" +
                    "• All brick details will be deleted\n\n" +
                    "This action is PERMANENT and CANNOT be undone!\n\n" +
                    "Click YES only if you are absolutely certain you want to proceed.",
                    "Clear Database - Final Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Stop);

                if (secondConfirm != MessageBoxResult.Yes)
                    return;

                // User confirmed twice, proceed with deletion
                _inventoryRepository.DeleteAllInventory();
                _brickRepository.DeleteAllBricks();
                _setRepository.DeleteAllSets();

                // Refresh the UI
                LoadSets();

                MessageBox.Show(
                    "Database has been cleared successfully.\n\nAll sets and inventory have been deleted.",
                    "Clear Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error clearing database: {ex.Message}",
                    "Clear Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ViewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isListView = !_isListView;

            if (_isListView)
            {
                // Switch to list view
                GalleryScrollViewer.Visibility = Visibility.Collapsed;
                ListDataGrid.Visibility = Visibility.Visible;
                ViewToggleIcon.Text = "🖼";
                ViewToggleText.Text = "Gallery View";
            }
            else
            {
                // Switch to gallery view
                GalleryScrollViewer.Visibility = Visibility.Visible;
                ListDataGrid.Visibility = Visibility.Collapsed;
                ViewToggleIcon.Text = "📋";
                ViewToggleText.Text = "List View";
            }
        }

        private void ListDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListDataGrid.SelectedItem is BrickSet set)
            {
                var setDetailsWindow = new SetDetailsWindow(set.SetNumber);
                setDetailsWindow.Owner = this;
                setDetailsWindow.ShowDialog();
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

        private void SetBrickIcon()
        {
            try
            {
                // Create a DrawingVisual to render the brick icon
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    // Define colors
                    SolidColorBrush brickColor = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // #E74C3C
                    SolidColorBrush borderColor = new SolidColorBrush(Color.FromRgb(192, 57, 43)); // #C0392B

                    // Draw brick body (32x24 pixels)
                    dc.DrawRectangle(brickColor, new Pen(borderColor, 2), new Rect(0, 8, 32, 24));

                    // Draw 4 studs on top (6x6 pixels each)
                    for (int i = 0; i < 4; i++)
                    {
                        dc.DrawEllipse(brickColor, new Pen(borderColor, 1), new Point(4 + i * 8, 6), 3, 3);
                    }
                }

                // Render to bitmap
                RenderTargetBitmap rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);

                // Convert to BitmapImage
                BitmapImage bitmapImage = new BitmapImage();
                using (MemoryStream stream = new MemoryStream())
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(stream);
                    stream.Position = 0;

                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }

                this.Icon = bitmapImage;
            }
            catch
            {
                // If icon creation fails, just continue without it
            }
        }
    }
}
