namespace Volt.Application.Dtos.Product
{
    public sealed record ProductParametrDto(
        string? TechnicalPower,
        decimal? Effectiveness,
        int? Count,
        decimal? Amount);
}
