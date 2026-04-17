using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Blog
{
    public sealed class BlogUpdateRequest
    {
        [Required]
        [MaxLength(500)]
        public string CoverImagePath { get; set; }

        [Required]
        [MinLength(1)]
        public List<BlogTranslationUpdateRequest> Translations { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
