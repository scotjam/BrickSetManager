using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows.Media.Imaging;
using BrickSetManager.Models;

namespace BrickSetManager.Database
{
    public class SetRepository
    {
        public void AddSet(BrickSet set)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = @"
                    INSERT OR REPLACE INTO SetsOwned (SetNumber, SetName, SetImage, BrickLinkURL, DateAdded, Quantity, InventoryVersion, ReleaseYear, PieceCount, HasMultipleVersions, SetLocation)
                    VALUES (@SetNumber, @SetName, @SetImage, @BrickLinkURL, @DateAdded, @Quantity, @InventoryVersion, @ReleaseYear, @PieceCount, @HasMultipleVersions, @SetLocation)";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", set.SetNumber);
                    cmd.Parameters.AddWithValue("@SetName", set.SetName);
                    cmd.Parameters.AddWithValue("@SetImage", set.SetImageData ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@BrickLinkURL", set.SetURL);
                    cmd.Parameters.AddWithValue("@DateAdded", set.DateAdded);
                    cmd.Parameters.AddWithValue("@Quantity", set.Quantity);
                    cmd.Parameters.AddWithValue("@InventoryVersion", set.InventoryVersion);
                    cmd.Parameters.AddWithValue("@ReleaseYear", set.ReleaseYear.HasValue ? (object)set.ReleaseYear.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@PieceCount", set.PieceCount.HasValue ? (object)set.PieceCount.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@HasMultipleVersions", set.HasMultipleVersions ? 1 : 0);
                    cmd.Parameters.AddWithValue("@SetLocation", string.IsNullOrEmpty(set.SetLocation) ? (object)DBNull.Value : set.SetLocation);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<BrickSet> GetAllSets()
        {
            var sets = new List<BrickSet>();

            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "SELECT SetNumber, SetName, SetImage, DateAdded, BrickLinkURL, Quantity, InventoryVersion, ReleaseYear, PieceCount, HasMultipleVersions, SetLocation FROM SetsOwned ORDER BY DateAdded DESC";

                using (var cmd = new SQLiteCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var set = new BrickSet
                        {
                            SetNumber = reader.GetString(0),
                            SetName = reader.GetString(1),
                            SetImageData = reader.IsDBNull(2) ? null : (byte[])reader["SetImage"],
                            DateAdded = reader.GetDateTime(3),
                            SetURL = reader.GetString(4),
                            Quantity = reader.GetInt32(5),
                            InventoryVersion = reader.GetInt32(6),
                            ReleaseYear = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                            PieceCount = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                            HasMultipleVersions = reader.IsDBNull(9) ? true : reader.GetInt32(9) == 1,
                            SetLocation = reader.IsDBNull(10) ? null : reader.GetString(10)
                        };

                        if (set.SetImageData != null)
                        {
                            set.SetImage = ByteArrayToBitmapImage(set.SetImageData);
                        }

                        sets.Add(set);
                    }
                }
            }

            return sets;
        }

        public BrickSet GetSet(string setNumber)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "SELECT SetNumber, SetName, SetImage, DateAdded, BrickLinkURL, Quantity, InventoryVersion, ReleaseYear, PieceCount, HasMultipleVersions, SetLocation FROM SetsOwned WHERE SetNumber = @SetNumber";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", setNumber);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var set = new BrickSet
                            {
                                SetNumber = reader.GetString(0),
                                SetName = reader.GetString(1),
                                SetImageData = reader.IsDBNull(2) ? null : (byte[])reader["SetImage"],
                                DateAdded = reader.GetDateTime(3),
                                SetURL = reader.GetString(4),
                                Quantity = reader.GetInt32(5),
                                InventoryVersion = reader.GetInt32(6),
                                ReleaseYear = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                PieceCount = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                                HasMultipleVersions = reader.IsDBNull(9) ? true : reader.GetInt32(9) == 1,
                                SetLocation = reader.IsDBNull(10) ? null : reader.GetString(10)
                            };

                            if (set.SetImageData != null)
                            {
                                set.SetImage = ByteArrayToBitmapImage(set.SetImageData);
                            }

                            return set;
                        }
                    }
                }
            }

            return null;
        }

        public void UpdateQuantity(string setNumber, int quantity)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "UPDATE SetsOwned SET Quantity = @Quantity WHERE SetNumber = @SetNumber";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", setNumber);
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateSetLocation(string setNumber, string location)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "UPDATE SetsOwned SET SetLocation = @Location WHERE SetNumber = @SetNumber";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", setNumber);
                    cmd.Parameters.AddWithValue("@Location", string.IsNullOrEmpty(location) ? (object)DBNull.Value : location);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteSet(string setNumber)
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "DELETE FROM SetsOwned WHERE SetNumber = @SetNumber";

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@SetNumber", setNumber);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteAllSets()
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                string query = "DELETE FROM SetsOwned";

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
