namespace Volt.Application.Dtos.ProductBrand
{
    public sealed record ProductBrandDto(
        int Id,
        int ProductCategoryId,
        string Name);
}
