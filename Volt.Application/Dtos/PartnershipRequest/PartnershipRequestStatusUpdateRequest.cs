using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.PartnershipRequest
{
    public sealed class PartnershipRequestStatusUpdateRequest
    {
        [Required]
        public byte Status { get; set; }
    }
}
