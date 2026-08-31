namespace Volt.Application.Dtos.PartnershipRequest
{
    public sealed record PartnershipRequestDto(
        int Id,
        string CompanyName,
        string CompanyPerson,
        string Email,
        string PhoneNumber,
        string Message,
        byte Status,
        int PartnershipTypeId,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        bool IsViewedByAdmin,
        DateTime? AdminViewedAt
    );
}
