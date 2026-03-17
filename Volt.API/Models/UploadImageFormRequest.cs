using System.ComponentModel.DataAnnotations;
using Volt.API.Infrastructure.Attributes;

namespace Volt.API.Models
{
    public class UploadImageFormRequest
    {
        [Required(ErrorMessage = "Fayl mütləqdir")]
        [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" })]
        public IFormFile File { get; set; }
    }
}
