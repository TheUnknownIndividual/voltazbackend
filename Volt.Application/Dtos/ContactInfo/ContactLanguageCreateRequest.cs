using System.ComponentModel.DataAnnotations;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed class ContactLanguageCreateRequest
    {
        [Required]
        public LanguageCode LanguageCode { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string WorkingHoursDescription { get; set; }
    }
}

