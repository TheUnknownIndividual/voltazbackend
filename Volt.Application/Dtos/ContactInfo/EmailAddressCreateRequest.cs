using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed class EmailAddressCreateRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}

