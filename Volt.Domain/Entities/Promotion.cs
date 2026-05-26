namespace Volt.Domain.Entities
{
    public class Promotion
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }

        public ICollection<ProductPromotion> ProductPromotions { get; set; } = new List<ProductPromotion>();
        public ICollection<PromotionLanguage> Languages { get; set; } = new List<PromotionLanguage>();
    }
}
