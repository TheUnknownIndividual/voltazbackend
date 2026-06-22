namespace Volt.Application.Dtos.Product
{
    public sealed class ProductParametrCreateRequest
    {
        public string? TechnicalPower { get; set; }
        public decimal? Effectiveness { get; set; }
        public int Count { get; set; }
        public decimal? Amount { get; set; }
    }
}
