using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class NewsPostLanguage
    {
        public int Id { get; set; }
        public int NewsPostId { get; set; }
        public NewsPost NewsPost { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Content { get; set; }
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
        public string SeoKeywords { get; set; }
        public bool IsActive { get; set; }
    }
}
