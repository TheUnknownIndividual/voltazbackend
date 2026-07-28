namespace Volt.Application.Dtos.Project
{
    public sealed record ProjectOfferDto(
        int Id,
        decimal Power,
        byte PowerType,
        string AreaType,
        bool IsActive
    );
}
