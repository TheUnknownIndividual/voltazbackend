namespace Volt.Application.Dtos.AdminProjectTracker;

/// <summary>Safe, bounded project data used only to format a stakeholder Telegram notification.</summary>
public sealed class StakeholderApprovalNotification
{
    public string SentBy { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = string.Empty;
    public decimal OfferPrice { get; init; }
    public DateTime? ProjectDate { get; init; }
    public DateTime? ResponseExpectedAt { get; init; }
    public string SmallNote { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<StakeholderApprovalOfferNotification> Offers { get; init; } = Array.Empty<StakeholderApprovalOfferNotification>();
    public IReadOnlyList<StakeholderApprovalAttachmentNotification> Attachments { get; init; } = Array.Empty<StakeholderApprovalAttachmentNotification>();
}

public sealed record StakeholderApprovalOfferNotification(decimal Power, string MountType, string AreaType, decimal ExtraAmount);
public sealed record StakeholderApprovalAttachmentNotification(string FileName, string FilePath, string Label);
