namespace Volt.Application.Dtos.PartnershipType
{
    public sealed record PartnershipTypeDto(
        int Id,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<PartnershipTypeLanguageDto> Languages
    );
}
