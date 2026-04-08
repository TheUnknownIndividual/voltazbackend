using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ApplicationType
{
    public sealed class ApplicationTypeUpdateRequest
    {
        public int? ServiceManagementId { get; set; }

        [Required]
        [MinLength(1)]
        public List<ApplicationTypeLanguageUpdateRequest> Languages { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

