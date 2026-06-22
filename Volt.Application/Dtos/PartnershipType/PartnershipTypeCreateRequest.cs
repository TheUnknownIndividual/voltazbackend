using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.PartnershipType
{
    public sealed class PartnershipTypeCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<PartnershipTypeLanguageCreateRequest> Languages { get; set; }
    }
}
