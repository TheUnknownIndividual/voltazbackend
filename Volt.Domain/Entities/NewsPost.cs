namespace Volt.Domain.Entities
{
    public class NewsPost
    {
        public int Id { get; set; }
        public string CoverImagePath { get; set; }
        public int CoverImagePositionX { get; set; } = 50;
        public int CoverImagePositionY { get; set; } = 50;
        public decimal CoverImageZoom { get; set; } = 1m;
        public string Source { get; set; }
        public string PostLink { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<NewsPostLanguage> Languages { get; set; } = new List<NewsPostLanguage>();
    }
}
