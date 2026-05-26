namespace Volt.Application.Dtos.Product
{
    public sealed record ProductDto(
        int Id,
        string ProductName,
        int ProductCategoryId,
        int ProductSubCategoryId,
        int ProductBrandId,
        int? ProductTechnologyId,
        bool InStock,
        bool InHomePage,
        string Certificate,
        IReadOnlyList<string> ProductImage,
        IReadOnlyList<string> ProductDatasheet,
        IReadOnlyList<ProductParametrDto> ProductParametrs,
        IReadOnlyList<ProductDescriptionDto> ProductDescriptions,
        IReadOnlyList<int> PromotionIds);
}
