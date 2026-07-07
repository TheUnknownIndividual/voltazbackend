using Volt.Application.Dtos.Product;

namespace Volt.Application.Dtos.Search
{
    public sealed record SearchProductGroupDto(
        IReadOnlyList<ProductDto> Items,
        int TotalCount);
}
