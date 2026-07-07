namespace Volt.Application.Dtos.Search
{
    public sealed record SearchUsefulPageResultDto(
        string Type,
        int? Id,
        string Title,
        string Description,
        string Page,
        string Route);
}
