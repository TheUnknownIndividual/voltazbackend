using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ApplicationType
{
    public sealed class ApplicationTypeCreateRequest
    {
        public int? ServiceManagementId { get; set; }

        [Required]
        [MinLength(1)]
        public List<ApplicationTypeLanguageCreateRequest> Languages { get; set; }
    }
}

