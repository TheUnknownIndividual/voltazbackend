namespace Volt.Domain.Entities;

/// <summary>Work assigned to a member of a delivery project.</summary>
public sealed class ExecutionProjectTask
{
    public int Id { get; set; }
    public int ExecutionProjectId { get; set; }
    public ExecutionProject ExecutionProject { get; set; } = null!;
    public int AssignedAdminUserId { get; set; }
    public AdminUser AssignedAdminUser { get; set; } = null!;
    public int CreatedByAdminUserId { get; set; }
    public AdminUser CreatedByAdminUser { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public string Status { get; set; } = "Assigned";
    // The row is an outbox record: a Telegram worker can deliver it safely later.
    public string NotificationStatus { get; set; } = "PendingRecipientLink";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
