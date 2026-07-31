namespace Volt.Domain.Entities;

/// <summary>
/// A short-lived, user-confirmed contact request created by a public AI agent.
/// It is deliberately separate from ContactRequst so unconfirmed personal data
/// never appears in the admin request queue.
/// </summary>
public sealed class PublicAgentContactDraft
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string AccessTokenHash { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int ApplicationTypeId { get; set; }
    public string Status { get; set; } = "PendingConfirmation";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? SubmittedContactRequestId { get; set; }
}
