using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.NewsPost
{
    public sealed class NewsPostUpdateRequest
    {
        [Required]
        [MaxLength(500)]
        public string CoverImagePath { get; set; }

        [Range(0, 100)]
        public int CoverImagePositionX { get; set; } = 50;

        [Range(0, 100)]
        public int CoverImagePositionY { get; set; } = 50;

        [Range(typeof(decimal), "1", "3")]
        public decimal CoverImageZoom { get; set; } = 1m;

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
