namespace SHMS.Backend.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; } // Medicine, Equipment, Consumable
        public int Quantity { get; set; }
        public int ThresholdValue { get; set; }
        public decimal Price { get; set; }
    }
}
