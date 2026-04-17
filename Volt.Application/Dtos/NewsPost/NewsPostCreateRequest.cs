using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.NewsPost
{
    public sealed class NewsPostCreateRequest
    {
        [Required]
        [MaxLength(500)]
        public string CoverImagePath { get; set; }

        [Required]
        [MaxLength(200)]
        public string Source { get; set; }

        [Required]
        [MaxLength(1000)]
        public string PostLink { get; set; }

        [Required]
        [MinLength(1)]
        public List<NewsPostLanguageCreateRequest> Languages { get; set; }
    }
}
