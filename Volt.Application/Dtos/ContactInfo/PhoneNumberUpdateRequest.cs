using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed class PhoneNumberUpdateRequest
    {
        [Required]
        public string Number { get; set; }
    }
}

