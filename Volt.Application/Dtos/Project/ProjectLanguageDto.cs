using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Project
{
    public sealed record ProjectLanguageDto(
        int Id,
        LanguageCode LanguageCode,
        string Title,
        string Description,
        string Location,
        bool IsActive
    );
}
