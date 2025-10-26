using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows.Media.Imaging;
using BrickSetManager.Models;

namespace BrickSetManager.Database
{
    public class BrickRepository
    {
        public void AddBrick(BrickDetail brick)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = @"
                    INSERT OR REPLACE INTO BrickDetails
                    (PartNumber, ColorID, ColorName, PartName, PartImage, PartURL, PriceGuideURL, Length, Width)
                    VALUES (@PartNumber, @ColorID, @ColorName, @PartName, @PartImage, @PartURL, @PriceGuideURL, @Length, @Width)";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@PartNumber", brick.PartNumber);
                    cmd.Parameters.AddWithValue("@ColorID", brick.ColorID);
                    cmd.Parameters.AddWithValue("@ColorName", brick.ColorName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@PartName", brick.PartName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@PartImage", brick.PartImageData ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PartURL", brick.PartURL ?? string.Empty);
                    cmd.Parameters.AddWithValue("@PriceGuideURL", brick.PriceGuideURL ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Length", brick.Length);
                    cmd.Parameters.AddWithValue("@Width", brick.Width);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public BrickDetail GetBrick(string partNumber, int colorID)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = @"
                    SELECT PartNumber, ColorID, ColorName, PartName, PartImage, PartURL, PriceGuideURL, Length, Width
                    FROM BrickDetails
                    WHERE PartNumber = @PartNumber AND ColorID = @ColorID";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@PartNumber", partNumber);
                    cmd.Parameters.AddWithValue("@ColorID", colorID);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
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
                                Width = reader.GetInt32(8)
                            };

                            if (brick.PartImageData != null)
                            {
                                brick.PartImage = ByteArrayToBitmapImage(brick.PartImageData);
                            }

                            return brick;
                        }
                    }
                }
            }

            return null;
        }

        public List<BrickDetail> GetBricksByColor(string colorName)
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
                    WHERE bd.ColorName = @ColorName
                    GROUP BY bd.PartNumber, bd.ColorID
                    ORDER BY bd.PartName";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@ColorName", colorName);

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
            }

            return bricks;
        }

        public List<string> GetAllColors()
        {
            var colors = new List<string>();

            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "SELECT DISTINCT ColorName FROM BrickDetails ORDER BY ColorName";

                using (var cmd = new SQLiteCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        colors.Add(reader.GetString(0));
                    }
                }
            }

            return colors;
        }

        public void DeleteAllBricks()
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "DELETE FROM BrickDetails";

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
