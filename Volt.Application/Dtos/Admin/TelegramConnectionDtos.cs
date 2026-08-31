namespace Volt.Application.Dtos.Admin;

public sealed record TelegramConnectionLinkDto(string Url, DateTime ExpiresAt, int AdminUserId, string LinkState);
public sealed record TelegramConnectionStatusDto(
    int AdminUserId,
    bool IsLinked,
    long? TelegramChatId,
    string LinkState,
    DateTime? LinkExpiresAt);
public sealed record TelegramConnectionRedeemRequest(string Token, long ChatId);
public sealed record TelegramAnalyticsSubscriptionRequest(long ChatId, string Topic, bool Enabled);
public sealed record TelegramAnalyticsSubscriptionStatusDto(bool Yoxla, bool Qiymetlendirme);

public sealed record TelegramManagementActorRequest(long ManagerChatId);
public sealed record TelegramManagementAssignRequest(long ManagerChatId, long TelegramChatId);
public sealed record TelegramManagementSubscriptionRequest(long ManagerChatId, string Topic, bool Enabled);
public sealed record TelegramManagedAdminDto(
    int Id,
    string Username,
    string DisplayName,
    bool IsActive,
    bool IsSuperAdmin,
    long? TelegramChatId,
    bool Yoxla,
    bool Qiymetlendirme);
