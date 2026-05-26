namespace Volt.Domain.Entities
{
    public class ProductDescription
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }

        public ICollection<ProductDescriptionLanguage> Languages { get; set; } = new List<ProductDescriptionLanguage>();
    }
}
