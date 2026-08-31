namespace Volt.Application.Dtos.ContactRequst
{
    public sealed record ContactRequstDto(
        int Id,
        string Name,
        string Surname,
        string Email,
        string Phone,
        string Message,
        DateTime CreatedAt,
        byte Status,
        bool IsActive,
        int ApplicationTypeId,
        bool IsViewedByAdmin,
        DateTime? AdminViewedAt
    );
}
