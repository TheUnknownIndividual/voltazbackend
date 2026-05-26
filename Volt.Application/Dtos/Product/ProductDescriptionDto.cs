namespace Volt.Application.Dtos.Product
{
    public sealed record ProductDescriptionDto(
        int Id,
        IReadOnlyList<ProductDescriptionLanguageDto> Languages);
}
