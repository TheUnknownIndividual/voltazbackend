using System.ComponentModel.DataAnnotations;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Blog
{
    public sealed class BlogTranslationCreateRequest
    {
        [Required]
        public LanguageCode LanguageCode { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; }

        [Required]
        public string Content { get; set; }

        [MaxLength(200)]
        public string SeoTitle { get; set; }

        [MaxLength(500)]
        public string SeoDescription { get; set; }

        [MaxLength(500)]
        public string SeoKeywords { get; set; }
    }
}
