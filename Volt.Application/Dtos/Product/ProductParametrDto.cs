namespace Volt.Application.Dtos.Product
{
    public sealed record ProductParametrDto(
        decimal? TechnicalPower,
        decimal? Effectiveness,
        int? Count,
        int? Amount);
}
