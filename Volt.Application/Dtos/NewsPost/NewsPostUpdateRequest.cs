using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.NewsPost
{
    public sealed class NewsPostUpdateRequest
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
        public List<NewsPostLanguageUpdateRequest> Languages { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
