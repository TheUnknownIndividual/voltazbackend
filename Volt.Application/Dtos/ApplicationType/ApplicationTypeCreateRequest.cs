using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ApplicationType
{
    public sealed class ApplicationTypeCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ApplicationTypeLanguageCreateRequest> Languages { get; set; }
    }
}

