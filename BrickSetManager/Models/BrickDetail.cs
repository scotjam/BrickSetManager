using System.Windows.Media.Imaging;

namespace BrickSetManager.Models
{
    public class BrickDetail
    {
        public string PartNumber { get; set; }
        public int ColorID { get; set; }
        public string ColorName { get; set; }
        public string PartName { get; set; }
        public byte[] PartImageData { get; set; }
        public BitmapImage PartImage { get; set; }
        public string PartURL { get; set; }
        public string PriceGuideURL { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int TotalQuantity { get; set; }
    }
}
