using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ContactInfo
{
    public sealed record ContactLanguageDto(
        int Id,
        LanguageCode LanguageCode,
        string Address,
        string WorkingHoursDescription
    );
}

