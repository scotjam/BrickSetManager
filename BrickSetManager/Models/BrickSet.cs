using System;
using System.Windows.Media.Imaging;

namespace BrickSetManager.Models
{
    public class BrickSet
    {
        public string SetNumber { get; set; }
        public string SetName { get; set; }
        public byte[] SetImageData { get; set; }
        public BitmapImage SetImage { get; set; }
        public DateTime DateAdded { get; set; }
        public string SetURL { get; set; }
        public int Quantity { get; set; } = 1;
        public int InventoryVersion { get; set; } = 1; // 1 = Old, 2+ = New
        public int? ReleaseYear { get; set; } // Year the set was released
        public int? PieceCount { get; set; } // Number of pieces in the set
        public bool HasMultipleVersions { get; set; } = true; // Whether multiple inventory versions exist
        public string SetLocation { get; set; } // Storage location (up to 100 characters)

        public string VersionBadge => InventoryVersion == 1 ? "vOld" : "vNew";
    }
}
