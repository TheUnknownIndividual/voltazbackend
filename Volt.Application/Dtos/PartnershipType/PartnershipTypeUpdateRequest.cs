using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.PartnershipType
{
    public sealed class PartnershipTypeUpdateRequest
    {
        [Required]
        [MinLength(1)]
        public List<PartnershipTypeLanguageUpdateRequest> Languages { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
