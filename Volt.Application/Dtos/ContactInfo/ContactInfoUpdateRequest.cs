using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed class ContactInfoUpdateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ContactLanguageUpdateRequest> Languages { get; set; }

        public List<PhoneNumberUpdateRequest>? PhoneNumbers { get; set; }
        public List<EmailAddressUpdateRequest>? EmailAddresses { get; set; }
    }
}

