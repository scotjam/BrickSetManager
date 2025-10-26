using System;
using System.Data.SQLite;
using System.IO;

namespace BrickSetManager.Database
{
    public static class DatabaseManager
    {
        private static string _dbPath;
        private static string _connectionString;

        public static string ConnectionString => _connectionString;

        public static void Initialize()
        {
            // Store database in user's AppData folder
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BrickSetManager");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _dbPath = Path.Combine(appDataPath, "BrickSets.db");
            _connectionString = $"Data Source={_dbPath};Version=3;";

            CreateDatabase();
        }

        private static void CreateDatabase()
        {
            bool isNewDatabase = !File.Exists(_dbPath);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                if (isNewDatabase)
                {
                    CreateTables(connection);
                }
                else
                {
                    // Run migrations for existing databases
                    MigrateDatabase(connection);
                }
            }
        }

        private static void MigrateDatabase(SQLiteConnection connection)
        {
            // Check which columns exist in SetsOwned table
            string checkColumnQuery = "PRAGMA table_info(SetsOwned)";
            bool hasQuantityColumn = false;
            bool hasInventoryVersionColumn = false;
            bool hasReleaseYearColumn = false;
            bool hasPieceCountColumn = false;
            bool hasHasMultipleVersionsColumn = false;
            bool hasSetLocationColumn = false;

            using (var cmd = new SQLiteCommand(checkColumnQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string columnName = reader.GetString(1); // Column name is at index 1
                    if (columnName.Equals("Quantity", StringComparison.OrdinalIgnoreCase))
                    {
                        hasQuantityColumn = true;
                    }
                    else if (columnName.Equals("InventoryVersion", StringComparison.OrdinalIgnoreCase))
                    {
                        hasInventoryVersionColumn = true;
                    }
                    else if (columnName.Equals("ReleaseYear", StringComparison.OrdinalIgnoreCase))
                    {
                        hasReleaseYearColumn = true;
                    }
                    else if (columnName.Equals("PieceCount", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPieceCountColumn = true;
                    }
                    else if (columnName.Equals("HasMultipleVersions", StringComparison.OrdinalIgnoreCase))
                    {
                        hasHasMultipleVersionsColumn = true;
                    }
                    else if (columnName.Equals("SetLocation", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSetLocationColumn = true;
                    }
                }
            }

            // Add Quantity column if it doesn't exist
            if (!hasQuantityColumn)
            {
                string addQuantityColumn = "ALTER TABLE SetsOwned ADD COLUMN Quantity INTEGER DEFAULT 1";
                using (var cmd = new SQLiteCommand(addQuantityColumn, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            // Add InventoryVersion column if it doesn't exist
            if (!hasInventoryVersionColumn)
            {
                string addInventoryVersionColumn = "ALTER TABLE SetsOwned ADD COLUMN InventoryVersion INTEGER DEFAULT 1";
                using (var cmd = new SQLiteCommand(addInventoryVersionColumn, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            // Add ReleaseYear column if it doesn't exist
            if (!hasReleaseYearColumn)
            {
                string addReleaseYearColumn = "ALTER TABLE SetsOwned ADD COLUMN ReleaseYear INTEGER";
                using (var cmd = new SQLiteCommand(addReleaseYearColumn, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            // Add PieceCount column if it doesn't exist
            if (!hasPieceCountColumn)
            {
                string addPieceCountColumn = "ALTER TABLE SetsOwned ADD COLUMN PieceCount INTEGER";
                using (var cmd = new SQLiteCommand(addPieceCountColumn, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            // Add HasMultipleVersions column if it doesn't exist
            if (!hasHasMultipleVersionsColumn)
            {
                string addHasMultipleVersionsColumn = "ALTER TABLE SetsOwned ADD COLUMN HasMultipleVersions INTEGER DEFAULT 1";
                using (var cmd = new SQLiteCommand(addHasMultipleVersionsColumn, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            // Add SetLocation column if it doesn't exist
            if (!hasSetLocationColumn)
            {
                string addSetLocationColumn = "ALTER TABLE SetsOwned ADD COLUMN SetLocation TEXT";
                using (var cmd = new SQLiteCommand(addSetLocationColumn, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void CreateTables(SQLiteConnection connection)
        {
            string createSetsOwnedTable = @"
                CREATE TABLE IF NOT EXISTS SetsOwned (
                    SetNumber TEXT PRIMARY KEY,
                    SetName TEXT NOT NULL,
                    SetImage BLOB,
                    DateAdded DATETIME DEFAULT CURRENT_TIMESTAMP,
                    SetURL TEXT,
                    Quantity INTEGER DEFAULT 1,
                    InventoryVersion INTEGER DEFAULT 1,
                    ReleaseYear INTEGER,
                    PieceCount INTEGER,
                    HasMultipleVersions INTEGER DEFAULT 1,
                    SetLocation TEXT
                );";

            string createBrickDetailsTable = @"
                CREATE TABLE IF NOT EXISTS BrickDetails (
                    PartNumber TEXT NOT NULL,
                    ColorID INTEGER NOT NULL,
                    ColorName TEXT,
                    PartName TEXT,
                    PartImage BLOB,
                    PartURL TEXT,
                    PriceGuideURL TEXT,
                    Length INTEGER,
                    Width INTEGER,
                    PRIMARY KEY (PartNumber, ColorID)
                );";

            string createSetInventoryTable = @"
                CREATE TABLE IF NOT EXISTS SetInventory (
                    SetNumber TEXT NOT NULL,
                    PartNumber TEXT NOT NULL,
                    ColorID INTEGER NOT NULL,
                    Quantity INTEGER NOT NULL,
                    PRIMARY KEY (SetNumber, PartNumber, ColorID),
                    FOREIGN KEY (SetNumber) REFERENCES SetsOwned(SetNumber) ON DELETE CASCADE,
                    FOREIGN KEY (PartNumber, ColorID) REFERENCES BrickDetails(PartNumber, ColorID)
                );";

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = createSetsOwnedTable;
                cmd.ExecuteNonQuery();

                cmd.CommandText = createBrickDetailsTable;
                cmd.ExecuteNonQuery();

                cmd.CommandText = createSetInventoryTable;
                cmd.ExecuteNonQuery();
            }
        }

        public static SQLiteConnection GetConnection()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
