using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed class EmailAddressUpdateRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}

