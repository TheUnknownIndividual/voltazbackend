namespace Volt.Domain.Entities
{
    public class Blog
    {
        public int Id { get; set; }
        public string CoverImagePath { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<BlogTranslation> Translations { get; set; } = new List<BlogTranslation>();
    }
}
