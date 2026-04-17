namespace Volt.Application.Dtos.Project
{
    public sealed record ProjectDto(
        int Id,
        int Position,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<ProjectLanguageDto> Languages,
        IReadOnlyList<ProjectImageDto> Images
    );
}
