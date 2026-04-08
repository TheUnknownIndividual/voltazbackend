using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ApplicationType
{
    public sealed record ApplicationTypeLanguageDto(
        int Id,
        LanguageCode LanguageCode,
        string Name,
        bool IsActive
    );
}

