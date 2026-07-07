namespace Volt.Application.Dtos.Search
{
    public sealed record SearchResultDto(
        SearchProductGroupDto Products,
        IReadOnlyList<SearchCategoryResultDto> Categories,
        IReadOnlyList<SearchServiceResultDto> Services,
        IReadOnlyList<SearchUsefulPageResultDto> UsefulPages);
}
