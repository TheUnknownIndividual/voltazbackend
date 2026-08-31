namespace Volt.Application.Dtos.ProdcutCategory;

public sealed record HomePageProductCategoryDto(
    int Id,
    string SeoKey,
    string Name,
    int ProductId,
    string ProductName,
    string ImageUrl,
    int DisplayOrder);
