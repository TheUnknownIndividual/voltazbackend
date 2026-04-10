using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed class PhoneNumberCreateRequest
    {
        [Required]
        public string Number { get; set; }
    }
}

