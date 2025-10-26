# Brick Set Manager

A Windows desktop application for managing your brick set collection with automatic inventory tracking.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-7.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)

## Features

- 📦 **Set Management**: Add, view, and organize your sets
- 🔍 **Automatic Inventory Scraping**: Fetches set details, parts, and images
- 🎨 **Color Search**: Find bricks by color across all your sets
- 📍 **Location Tracking**: Keep track of where each set is stored (up to 100 characters)
- 🔢 **Quantity Management**: Track multiple copies of the same set
- 📊 **Multiple Views**: Gallery view with images or sortable list view
- 🏷️ **Version Tracking**: Support for old and new inventory versions
- 💾 **Local Database**: All data stored locally in SQLite
- 🖼️ **Image Caching**: Set and part images stored locally for fast access
- 🔎 **Smart Search**: Word-based search across set names, numbers, and part names
- 📈 **Statistics**: Total sets counter showing unique sets and total quantity

## Requirements

### For Running the Installer
- **Windows 10/11** (64-bit)
- No additional dependencies (self-contained)

### For Building from Source
- **Windows 10/11** (64-bit)
- **.NET 7.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/7.0)
- **Visual Studio 2022** (optional, recommended for development)
  - Workload: ".NET desktop development"
- **Inno Setup 6** (for creating installer) - [Download](https://jrsoftware.org/isinfo.php)

## Installation

### Option 1: Using the Installer (Recommended)

1. Download `BrickSetManagerSetup.exe` from the releases
2. Run the installer
3. Follow the installation wizard
4. Launch "Brick Set Manager" from the Start Menu

### Option 2: Running from Source

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd BrickSetManager
   ```

2. **Navigate to the main project directory**
   ```bash
   cd BrickSetManager
   ```

3. **Restore dependencies**
   ```bash
   dotnet restore
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

## Building from Source

### Debug Build

```bash
cd BrickSetManager
dotnet build
```

The executable will be in `bin\Debug\net7.0-windows\BrickSetManager.exe`

### Release Build

```bash
cd BrickSetManager
dotnet build -c Release
```

The executable will be in `bin\Release\net7.0-windows\BrickSetManager.exe`

## Creating the Installer

### Prerequisites
- Inno Setup 6 must be installed at `C:\Program Files (x86)\Inno Setup 6\`
- The application must be published first

### Quick Method (Command Line)

```bash
# From the repository root directory
cd BrickSetManager
dotnet publish -c Release -r win-x64 --self-contained true
cd ..\Installer
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

The installer will be created at `Installer\Output\BrickSetManagerSetup.exe` (~49MB)

### Step-by-Step Method

1. **Publish the application** (creates self-contained package):
   ```bash
   cd BrickSetManager
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

   This creates a self-contained release in:
   ```
   bin\Release\net7.0-windows\win-x64\publish\
   ```

2. **Compile the installer using Inno Setup**:

   **Option A - Command Line**:
   ```bash
   cd ..\Installer
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
   ```

   **Option B - GUI**:
   - Open `Installer\setup.iss` in Inno Setup Compiler
   - Click Build → Compile
   - Wait for compilation to complete

3. **Find the installer**:
   - Location: `Installer\Output\BrickSetManagerSetup.exe`
   - Size: ~49MB (self-contained, includes .NET 7.0 runtime)

### Why is the installer 49MB?

The installer is self-contained, meaning it includes:
- The entire .NET 7.0 runtime
- WPF framework libraries
- Your application and all dependencies

**Advantage**: Users don't need to install .NET 7.0 separately - just run the installer and go!

## Project Structure

```
BrickSetManager/                  # Repository root
├── BrickSetManager/              # Main WPF application
│   ├── Database/                 # SQLite database layer
│   │   ├── DatabaseManager.cs   # Database initialization & migrations
│   │   ├── SetRepository.cs     # Set data access
│   │   ├── InventoryRepository.cs
│   │   └── BrickRepository.cs
│   ├── Models/                   # Data models
│   │   ├── BrickSet.cs
│   │   ├── BrickDetail.cs
│   │   └── InventoryItem.cs
│   ├── Services/                 # Business logic
│   │   ├── Scraper.cs           # Web scraping service
│   │   ├── ImageDownloader.cs
│   │   └── SetQueueProcessor.cs # Background queue processing
│   ├── Windows/                  # Additional windows
│   │   ├── AddSetWindow.xaml
│   │   ├── SetDetailsWindow.xaml
│   │   ├── SearchColorWindow.xaml
│   │   └── BricksWindow.xaml
│   ├── Converters/              # WPF value converters
│   └── MainWindow.xaml          # Main application window
├── Installer/                    # Inno Setup installer configuration
│   ├── setup.iss                # Installer script
│   └── Output/                  # Generated installers
├── DebugScraper/                # Standalone scraper testing tool
└── README.md                    # This file
```

## Database

The application uses SQLite for local storage. The database is automatically created on first run at:

```
%APPDATA%\BrickSetManager\BrickSets.db
```

### Database Schema

**SetsOwned**: Stores set information
- SetNumber (PRIMARY KEY)
- SetName
- SetImage (BLOB)
- DateAdded
- BrickLinkURL
- Quantity
- InventoryVersion
- ReleaseYear
- PieceCount
- HasMultipleVersions
- SetLocation

**BrickDetails**: Stores unique brick information
- PartNumber (PRIMARY KEY)
- ColorID (PRIMARY KEY)
- ColorName
- PartName
- PartImage (BLOB)
- PartURL
- PriceGuideURL
- Length
- Width

**SetInventory**: Links sets to their bricks
- SetNumber (FOREIGN KEY)
- PartNumber (FOREIGN KEY)
- ColorID (FOREIGN KEY)
- Quantity

## Usage

### Adding a Set

1. Click **"Add Set"** button in the toolbar
2. Enter the set number:
   - Format: `21044` or `21044-1`
   - If you omit `-1`, it will be added automatically
3. The application will automatically:
   - Fetch set details
   - Download set and part images
   - Parse the complete inventory
   - Detect if multiple inventory versions exist
   - Extract release year and piece count
   - Store everything in the local database

### Viewing Sets

**Gallery View** (Default):
- Large set images with badges
- Shows quantity badge (e.g., "x2" if you own 2 copies)
- Shows version badge (vOld/vNew) only if multiple versions exist
- Click any set to view details

**List View**:
- Sortable table with columns:
  - Image thumbnail
  - Set Number
  - Set Name
  - Pieces
  - Year
  - Version
  - Location
  - Date Added
- Click column headers to sort
- Double-click a row to view details

**Toggle Views**: Click the "Toggle View" button in the toolbar

### Viewing Set Details

1. Click on any set (gallery view) or double-click (list view)
2. View complete inventory with:
   - Part images
   - Part names and numbers
   - Quantities
   - Colors
   - Total part count
3. Search within the inventory using the search box
4. Click set name or number to open on BrickLink
5. Edit location directly in the details window

### Searching by Color

1. Click **"Search"** button in the toolbar
2. Select a color from the dropdown:
   - Choose "Any color" to search across all colors
   - Or select a specific color
3. Optionally filter by part name/number in the search box
4. Click **"Search"** to view results
5. Results show total count (e.g., "Found 45 brick(s) in Red")

### Managing Quantities

**Right-click any set** (works in both gallery and list view):
- **Add Copy**: Increase quantity by 1
- **Remove Copy**: Decrease quantity by 1
- **Set Quantity...**: Enter a specific quantity

### Setting Locations

Track where each set is physically stored (up to 100 characters):

**Method 1 - Context Menu**:
- Right-click any set → **"Set Location..."**
- Enter location (e.g., "Shelf A3", "Basement bin #5")

**Method 2 - Set Details**:
- Open set details window
- Edit the location field directly
- Changes save automatically when you leave the field

### Searching Sets

Use the search box in the toolbar to find sets:
- Searches set names and numbers
- **Word-based search**: Each word is searched independently
- Example: "castle dragon" finds any set containing both "castle" AND "dragon"
- Results update as you type

### Managing Database

**Clear Database**:
1. Click **"Clear DB"** button (red button in toolbar)
2. First confirmation: Review what will be deleted
3. Second confirmation: Final warning
4. All sets, inventory, and bricks are permanently deleted

**Note**: This action cannot be undone! The database will be empty but the structure remains.

## Development

### Debug Scraper

The `DebugScraper` project is a console application for testing the scraper independently:

```bash
cd DebugScraper
dotnet run
```

This will:
1. Fetch a test set
2. Display extracted data (name, year, pieces, multiple versions)
3. Save the HTML to a file for inspection
4. Count the number of parts found
5. Show sample inventory items

### Project Dependencies

- **System.Data.SQLite.Core**: SQLite database for .NET
- **HtmlAgilityPack**: HTML parsing and web scraping
- **.NET 7.0 Windows Desktop Runtime**: WPF framework

### Adding Features

The codebase follows a clear separation of concerns:

- **Database layer**: All database operations in `*Repository.cs` files
- **Business logic**: Services for scraping, image downloading, and queue processing
- **UI**: XAML for layout, code-behind for event handlers
- **Models**: Simple POCOs for data transfer

### Database Migrations

The database automatically migrates on startup. To add new columns:

1. Edit `DatabaseManager.cs`
2. Add column check in `MigrateDatabase()`:
   ```csharp
   bool hasYourNewColumn = false;
   // Add check in the while (reader.Read()) loop
   ```
3. Add migration logic:
   ```csharp
   if (!hasYourNewColumn)
   {
       string addColumn = "ALTER TABLE SetsOwned ADD COLUMN YourColumn TEXT";
       using (var cmd = new SQLiteCommand(addColumn, connection))
       {
           cmd.ExecuteNonQuery();
       }
   }
   ```
4. Update `CreateTables()` to include the column for new databases

## Troubleshooting

### Application Won't Start
- Ensure you're running Windows 10/11 (64-bit)
- Try running as administrator
- Check Windows Event Viewer for error details

### Sets Won't Download
- Check your internet connection
- Verify the set exists (try opening the URL manually)
- HTML structure may have changed
- Try again later (site may be temporarily unavailable)

### Images Not Displaying
- Images are downloaded on first add
- If images fail to download, they won't display
- Delete the set and re-add it to try downloading again

### Database Issues
- Close the application
- Delete the database file: `%APPDATA%\BrickSetManager\BrickSets.db`
- Restart the application (it will recreate the database with the latest schema)

### Build Errors
- Ensure .NET 7.0 SDK is installed: `dotnet --version` should show 7.0.x
- Clean and rebuild: `dotnet clean && dotnet build`
- Restore packages: `dotnet restore`
- Check that all NuGet packages are restored

### Installer Creation Fails
- Verify Inno Setup 6 is installed at the expected path
- Ensure the publish step completed successfully
- Check that `bin\Release\net7.0-windows\win-x64\publish\` exists and contains files
- Try running ISCC.exe directly to see detailed error messages

## Technical Details

### Web Scraping Behavior

The scraper mimics natural browser behavior:
- Downloads HTML pages sequentially (set page, then inventory page)
- Natural delays between requests (500-1500ms)
- Downloads images in batches of 6 (like browser parallel connections)
- Small delays between image batches (100-300ms)
- User-Agent header matches common browsers

### HTML Entity Decoding

Set names with special characters are properly decoded:
- `&amp;` → `&`
- `&aacute;` → `á`
- `&eacute;` → `é`
- All other HTML entities

### Multiple Version Detection

The scraper automatically detects if a set has multiple inventory versions by:
- Checking the latest inventory page for "Older version" links
- Setting `HasMultipleVersions` flag in database
- Showing version badges only when multiple versions exist

## License

This project is provided as-is for personal use. BrickLink and LEGO are trademarks of their respective owners.

## Acknowledgments

- **BrickLink**: Data source for set and part information
- **HtmlAgilityPack**: HTML parsing library
- **SQLite**: Embedded database engine
- **Inno Setup**: Installer creation tool

---

**Disclaimer**: This application is not affiliated with or endorsed by the LEGO Group or BrickLink. It is an independent tool created by fans for managing personal LEGO collections. Please respect BrickLink's terms of service and use the scraping functionality responsibly.
