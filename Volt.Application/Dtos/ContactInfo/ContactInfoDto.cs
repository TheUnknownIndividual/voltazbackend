namespace Volt.Application.Dtos.ContactInfo
{
    public sealed record ContactInfoDto(
        int Id,
        IReadOnlyList<ContactLanguageDto> Languages,
        IReadOnlyList<PhoneNumberDto> PhoneNumbers,
        IReadOnlyList<EmailAddressDto> EmailAddresses
    );
}

