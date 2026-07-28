namespace Volt.Application.Dtos.Admin
{
    public sealed record AdminUserActivityDto(
        long Id,
        string Action,
        string? TargetType,
        string? TargetId,
        string? Summary,
        bool Succeeded,
        DateTime CreatedAt);

    public sealed record AdminUserActivityPageDto(
        IReadOnlyList<AdminUserActivityDto> Items,
        int TotalCount,
        int Page,
        int PageSize);
}
