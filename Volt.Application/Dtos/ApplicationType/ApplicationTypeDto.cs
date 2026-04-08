namespace Volt.Application.Dtos.ApplicationType
{
    public sealed record ApplicationTypeDto(
        int Id,
        int? ServiceManagementId,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<ApplicationTypeLanguageDto> Languages
    );
}

