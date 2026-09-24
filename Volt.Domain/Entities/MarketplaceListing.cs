namespace Volt.Domain.Entities
{
    // Records where a product has been posted on an external marketplace (lalafo, tapaz).
    // Deliberately has no foreign key to Products so it can never affect product deletes.
    public sealed class MarketplaceListing
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Marketplace { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string Status { get; set; } = "draft";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
