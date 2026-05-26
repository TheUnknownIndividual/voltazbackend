namespace Volt.Application.Dtos.Product
{
    public sealed class ProductParametrCreateRequest
    {
        public decimal? TechnicalPower { get; set; }
        public decimal? Effectiveness { get; set; }
        public int Count { get; set; }
        public int Amount { get; set; }
    }
}
