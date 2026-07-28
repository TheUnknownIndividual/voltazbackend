namespace Volt.Application.Dtos.Verification
{
    public sealed record PublicDocumentVerificationDto(
        bool IsOfficialVoltDocument,
        string DocumentNumber,
        string DocumentCode,
        string Status,
        DateTime IssuedAt,
        DateTime ExpiresAt,
        string Salesperson);

    public sealed record AdminDocumentVerificationDto(
        int Id,
        int DocumentLogId,
        string PublicToken,
        string PublicUrl,
        string DocumentNumber,
        string DocumentCode,
        string IssuerDisplayName,
        DateTime IssuedAt,
        DateTime ExpiresAt,
        DateTime? RevokedAt,
        int? AdminTrackedProjectId,
        string? AdminTrackedProjectName,
        string? RevocationReason);

    public sealed class LinkVerificationProjectRequest
    {
        public int AdminTrackedProjectId { get; set; }
    }

    public sealed class RevokeDocumentVerificationRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class PublicDocumentVerificationInquiryRequest
    {
        public string Type { get; set; } = string.Empty;
        public string? Comment { get; set; }
    }

    public sealed record PublicDocumentVerificationInquiryDto(int Id, string Status);

    public class VerificationInquiryListItemDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int? AssignedAdminUserId { get; set; }
        public string? AssignedAdminDisplayName { get; set; }
        public int DocumentLogId { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocumentCode { get; set; } = string.Empty;
        public string VerificationStatus { get; set; } = string.Empty;
        public int? AdminTrackedProjectId { get; set; }
        public string? AdminTrackedProjectName { get; set; }
    }

    public sealed class VerificationInquiryDetailDto : VerificationInquiryListItemDto
    {
        public string IssuerDisplayName { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevocationReason { get; set; }
        public string DocumentPayloadJson { get; set; } = string.Empty;
        public object? Project { get; set; }
    }

    public sealed class VerificationInquiryPageDto
    {
        public IReadOnlyList<VerificationInquiryListItemDto> Items { get; set; } = Array.Empty<VerificationInquiryListItemDto>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public sealed class AssignVerificationInquiryRequest
    {
        public int? AssignedAdminUserId { get; set; }
    }

    public sealed class UpdateVerificationInquiryStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
