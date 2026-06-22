namespace Volt.Domain.Entities
{
    public class ProductParametr
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public string? TechnicalPower { get; set; }
        public decimal? Effectiveness { get; set; }
        public int? Count { get; set; }
        public decimal? Amount { get; set; }
        public bool IsActive { get; set; }
    }
}
