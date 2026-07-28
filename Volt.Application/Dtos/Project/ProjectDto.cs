namespace Volt.Application.Dtos.Project
{
    public sealed record ProjectDto(
        int Id,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        int TotalPower,
        byte PowerType,
        int AnnualProduction,
        byte AnnualProductionType,
        byte SystemType,
        string? ContactFullName,
        string? ContactPhone,
        DateTime? ProjectDate,
        DateTime? InquiryReceivedAt,
        DateTime? OfferSentAt,
        DateTime? ResponseExpectedAt,
        string? CurrentStatus,
        string? ShortNote,
        decimal? OfferAmountAzn,
        IReadOnlyList<ProjectLanguageDto> Languages,
        IReadOnlyList<ProjectImageDto> Images,
        IReadOnlyList<ProjectAttachmentDto> Attachments,
        IReadOnlyList<ProjectOfferDto> Offers
    );
}
