namespace Volt.Application.Dtos.Product
{
    public sealed record ProductParametrDto(
        int Id,
        string? ModelLabel,
        string? TechnicalPower,
        decimal? Effectiveness,
        int? Count,
        decimal? Amount,
        IReadOnlyList<ProductParametrLanguageDto> Languages);
}
