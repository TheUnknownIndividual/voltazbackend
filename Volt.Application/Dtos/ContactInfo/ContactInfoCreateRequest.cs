using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed class ContactInfoCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ContactLanguageCreateRequest> Languages { get; set; }

        public List<PhoneNumberCreateRequest>? PhoneNumbers { get; set; }
        public List<EmailAddressCreateRequest>? EmailAddresses { get; set; }
    }
}

