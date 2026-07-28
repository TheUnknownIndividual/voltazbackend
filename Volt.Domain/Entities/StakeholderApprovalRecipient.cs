namespace Volt.Domain.Entities;

/// <summary>One intended Telegram recipient and the safe outcome of sending to that chat.</summary>
public sealed class StakeholderApprovalRecipient
{
    public int Id { get; set; }
    public int StakeholderApprovalRequestId { get; set; }
    public StakeholderApprovalRequest StakeholderApprovalRequest { get; set; } = null!;
    public int AdminUserId { get; set; }
    public long TelegramChatId { get; set; }
    public string DeliveryStatus { get; set; } = "Dispatching";
    public DateTime? DeliveredAt { get; set; }
    public string FailureStatus { get; set; } = string.Empty;
}
