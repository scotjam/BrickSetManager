namespace BrickSetManager.Models
{
    public class InventoryItem
    {
        public string SetNumber { get; set; }
        public string PartNumber { get; set; }
        public int ColorID { get; set; }
        public int Quantity { get; set; }
        public BrickDetail BrickDetail { get; set; }
    }
}
