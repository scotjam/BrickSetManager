using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows.Media.Imaging;
using BrickSetManager.Models;

namespace BrickSetManager.Database
{
    public class InventoryRepository
    {
        private readonly BrickRepository _brickRepository;

        public InventoryRepository()
        {
            _brickRepository = new BrickRepository();
        }

        public void AddInventoryItem(InventoryItem item)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = @"
                    INSERT OR REPLACE INTO SetInventory (SetNumber, PartNumber, ColorID, Quantity)
                    VALUES (@SetNumber, @PartNumber, @ColorID, @Quantity)";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", item.SetNumber);
                    cmd.Parameters.AddWithValue("@PartNumber", item.PartNumber);
                    cmd.Parameters.AddWithValue("@ColorID", item.ColorID);
                    cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<InventoryItem> GetSetInventory(string setNumber)
        {
            var items = new List<InventoryItem>();

            using (var connection = DatabaseManager.GetConnection())
            {
                string query = @"
                    SELECT SetNumber, PartNumber, ColorID, Quantity
                    FROM SetInventory
                    WHERE SetNumber = @SetNumber
                    ORDER BY PartNumber";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", setNumber);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new InventoryItem
                            {
                                SetNumber = reader.GetString(0),
                                PartNumber = reader.GetString(1),
                                ColorID = reader.GetInt32(2),
                                Quantity = reader.GetInt32(3)
                            };

                            // Load the brick details
                            item.BrickDetail = _brickRepository.GetBrick(item.PartNumber, item.ColorID);

                            items.Add(item);
                        }
                    }
                }
            }

            return items;
        }

        public List<InventoryItem> GetSetsContainingBrick(string partNumber, int colorID)
        {
            var items = new List<InventoryItem>();

            using (var connection = DatabaseManager.GetConnection())
            {
                string query = @"
                    SELECT si.SetNumber, si.PartNumber, si.ColorID, si.Quantity, so.SetName
                    FROM SetInventory si
                    INNER JOIN SetsOwned so ON si.SetNumber = so.SetNumber
                    WHERE si.PartNumber = @PartNumber AND si.ColorID = @ColorID
                    ORDER BY so.SetName";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@PartNumber", partNumber);
                    cmd.Parameters.AddWithValue("@ColorID", colorID);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new InventoryItem
                            {
                                SetNumber = reader.GetString(0),
                                PartNumber = reader.GetString(1),
                                ColorID = reader.GetInt32(2),
                                Quantity = reader.GetInt32(3)
                            };

                            items.Add(item);
                        }
                    }
                }
            }

            return items;
        }

        public void DeleteSetInventory(string setNumber)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "DELETE FROM SetInventory WHERE SetNumber = @SetNumber";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", setNumber);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<BrickDetail> GetAllBricksWithQuantity()
        {
            var bricks = new List<BrickDetail>();

            using (var connection = DatabaseManager.GetConnection())
            {
                string query = @"
                    SELECT DISTINCT bd.PartNumber, bd.ColorID, bd.ColorName, bd.PartName,
                           bd.PartImage, bd.PartURL, bd.PriceGuideURL, bd.Length, bd.Width,
                           SUM(si.Quantity * so.Quantity) as TotalQuantity
                    FROM BrickDetails bd
                    INNER JOIN SetInventory si ON bd.PartNumber = si.PartNumber AND bd.ColorID = si.ColorID
                    INNER JOIN SetsOwned so ON si.SetNumber = so.SetNumber
                    GROUP BY bd.PartNumber, bd.ColorID
                    ORDER BY bd.PartName";

                using (var cmd = new SQLiteCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var brick = new BrickDetail
                        {
                            PartNumber = reader.GetString(0),
                            ColorID = reader.GetInt32(1),
                            ColorName = reader.GetString(2),
                            PartName = reader.GetString(3),
                            PartImageData = reader.IsDBNull(4) ? null : (byte[])reader["PartImage"],
                            PartURL = reader.GetString(5),
                            PriceGuideURL = reader.GetString(6),
                            Length = reader.GetInt32(7),
                            Width = reader.GetInt32(8),
                            TotalQuantity = reader.GetInt32(9)
                        };

                        if (brick.PartImageData != null)
                        {
                            brick.PartImage = ByteArrayToBitmapImage(brick.PartImageData);
                        }

                        bricks.Add(brick);
                    }
                }
            }

            return bricks;
        }

        public void DeleteAllInventory()
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "DELETE FROM SetInventory";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private BitmapImage ByteArrayToBitmapImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return null;

            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
    }
}
