namespace Volt.Application.Dtos.ApplicationType
{
    public sealed record ApplicationTypeDto(
        int Id,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<ApplicationTypeLanguageDto> Languages
    );
}

