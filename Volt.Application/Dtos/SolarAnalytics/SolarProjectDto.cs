namespace Volt.Application.Dtos.SolarAnalytics
{
    public sealed record SolarProjectDto(
        int Id,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}
