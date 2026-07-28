namespace Volt.Domain.Entities;

/// <summary>
/// Immutable-in-intent audit record for one stakeholder decision round. Delivery proof is
/// kept in <see cref="StakeholderApprovalRecipient"/> rather than inferred from a project flag.
/// </summary>
public sealed class StakeholderApprovalRequest
{
    public int Id { get; set; }
    public int AdminTrackedProjectId { get; set; }
    public AdminTrackedProject AdminTrackedProject { get; set; } = null!;
    public string EnvironmentScope { get; set; } = "prod";
    public string Status { get; set; } = "Dispatching";
    public DateTime CreatedAt { get; set; }
    public DateTime? DispatchCompletedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByAdminUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<StakeholderApprovalRecipient> Recipients { get; set; } = new List<StakeholderApprovalRecipient>();
}
