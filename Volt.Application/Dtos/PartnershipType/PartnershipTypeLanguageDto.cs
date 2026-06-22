using Volt.Domain.Enums;

namespace Volt.Application.Dtos.PartnershipType
{
    public sealed record PartnershipTypeLanguageDto(
        int Id,
        LanguageCode LanguageCode,
        string Name,
        bool IsActive
    );
}
