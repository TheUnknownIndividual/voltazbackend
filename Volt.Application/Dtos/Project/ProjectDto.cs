namespace Volt.Application.Dtos.Project
{
    public sealed record ProjectDto(
        int Id,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        int TotalPower,
        byte PowerType,
        int AnnualProduction,
        byte AnnualProductionType,
        byte SystemType,
        IReadOnlyList<ProjectLanguageDto> Languages,
        IReadOnlyList<ProjectImageDto> Images
    );
}
