namespace Volt.Application.Dtos.Search
{
    public sealed record SearchCategoryResultDto(
        string Type,
        int ProductCategoryId,
        int? ProductSubCategoryId,
        string Name);
}
