using System.ComponentModel.DataAnnotations;
using Volt.API.Infrastructure.Attributes;

namespace Volt.API.Models
{
    public class UploadPdfFormRequest
    {
        [Required(ErrorMessage = "Fayl mütləqdir")]
        [AllowedExtensions(new[] { ".pdf", ".docx" })]
        public IFormFile File { get; set; }
    }
}
