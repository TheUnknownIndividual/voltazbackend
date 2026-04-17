namespace Volt.Domain.Entities
{
    public class NewsPost
    {
        public int Id { get; set; }
        public string CoverImagePath { get; set; }
        public string Source { get; set; }
        public string PostLink { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<NewsPostLanguage> Languages { get; set; } = new List<NewsPostLanguage>();
    }
}
