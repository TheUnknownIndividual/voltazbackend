namespace Volt.Domain.Entities
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int? ProductParametrId { get; set; }
        public string ProductName { get; set; }
        public string ProductImageUrl { get; set; }
        public string SelectedPower { get; set; }
        public int Quantity { get; set; }
        public int ReservedQuantity { get; set; }
        public bool InventoryReleased { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
