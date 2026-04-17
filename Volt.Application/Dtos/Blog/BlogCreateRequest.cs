using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Blog
{
    public sealed class BlogCreateRequest
    {
        [Required]
        [MaxLength(500)]
        public string CoverImagePath { get; set; }

        [Required]
        [MinLength(1)]
        public List<BlogTranslationCreateRequest> Translations { get; set; }
    }
}
