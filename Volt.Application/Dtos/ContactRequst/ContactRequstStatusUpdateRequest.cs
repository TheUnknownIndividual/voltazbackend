using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactRequst
{
    public sealed class ContactRequstStatusUpdateRequest
    {
        [Required]
        public byte Status { get; set; }
    }
}
