namespace Volt.Domain.Entities;

/// <summary>One-time, expiring token redeemed by the Telegram bot to link a private chat.</summary>
public sealed class AdminTelegramConnectionToken
{
    public int Id { get; set; }
    public int AdminUserId { get; set; }
    public AdminUser AdminUser { get; set; } = null!;
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();
    public DateTime ExpiresAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public long? RedeemedChatId { get; set; }
    public DateTime CreatedAt { get; set; }
}
