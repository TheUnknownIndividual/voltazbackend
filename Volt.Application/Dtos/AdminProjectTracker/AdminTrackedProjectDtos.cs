namespace Volt.Application.Dtos.AdminProjectTracker
{
    public sealed class AdminTrackedProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public IReadOnlyList<AdminTrackedProjectOfferDto> Offers { get; set; } = Array.Empty<AdminTrackedProjectOfferDto>();
        public string Location { get; set; } = string.Empty;
        public string PersonName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime? ProjectDate { get; set; }
        public DateTime? InquiryReceivedAt { get; set; }
        public DateTime? OfferSentAt { get; set; }
        public DateTime? ResponseExpectedAt { get; set; }
        public byte? SystemType { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
        public string SmallNote { get; set; } = string.Empty;
        public decimal OfferPrice { get; set; }
        public bool IsOfferPriceManual { get; set; }
        public bool IncludesAdv { get; set; }
        public string Description { get; set; } = string.Empty;
        public string StakeholderApprovalStatus { get; set; } = "NotRequired";
        public DateTime? StakeholderApprovalRequestedAt { get; set; }
        public DateTime? StakeholderApprovalResolvedAt { get; set; }
        public int? StakeholderApprovalRequestId { get; set; }
        public int StakeholderApprovalDeliveredRecipientCount { get; set; }
        public int StakeholderApprovalFailedRecipientCount { get; set; }
        public IReadOnlyList<string> StakeholderApprovalFailureStatuses { get; set; } = Array.Empty<string>();
        public IReadOnlyList<AdminTrackedProjectAttachmentDto> Attachments { get; set; } = Array.Empty<AdminTrackedProjectAttachmentDto>();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class AdminTrackedProjectOfferDto
    {
        public int Id { get; set; }
        public decimal Power { get; set; }
        public string MountType { get; set; } = string.Empty;
        public string AreaType { get; set; } = string.Empty;
        public decimal ExtraAmount { get; set; }
        public DateTime? SentAt { get; set; }
    }

    public sealed class AdminTrackedProjectAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public sealed class AdminTrackedProjectUpsertRequest
    {
        public string Name { get; set; } = string.Empty;
        public List<AdminTrackedProjectOfferRequest> Offers { get; set; } = new();
        public string Location { get; set; } = string.Empty;
        public string PersonName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime? ProjectDate { get; set; }
        public DateTime? InquiryReceivedAt { get; set; }
        public DateTime? OfferSentAt { get; set; }
        public DateTime? ResponseExpectedAt { get; set; }
        public byte? SystemType { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
        public string SmallNote { get; set; } = string.Empty;
        public decimal OfferPrice { get; set; }
        public bool IsOfferPriceManual { get; set; }
        public bool IncludesAdv { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<AdminTrackedProjectAttachmentRequest> Attachments { get; set; } = new();
    }

    public sealed class AdminTrackedProjectOfferRequest
    {
        public decimal Power { get; set; }
        public string MountType { get; set; } = string.Empty;
        public string AreaType { get; set; } = string.Empty;
        public decimal ExtraAmount { get; set; }
        public DateTime? SentAt { get; set; }
    }

    public sealed class AdminTrackedProjectAttachmentRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
