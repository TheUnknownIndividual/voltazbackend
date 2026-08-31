namespace Volt.Application.Dtos.Product
{
    public sealed class ProductParametrCreateRequest
    {
        public int? Id { get; set; }
        public string? ModelLabel { get; set; }
        public string? TechnicalPower { get; set; }
        public decimal? Effectiveness { get; set; }
        public int Count { get; set; }
        public decimal? Amount { get; set; }
        public List<ProductParametrLanguageCreateRequest> Languages { get; set; } = new();
    }
}
