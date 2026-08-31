using Volt.Application.Dtos;
using Volt.Application.Dtos.Admin;

namespace Volt.Application.Interfaces;

public interface IAdminTelegramConnectionService
{
    Task<ApiResponse<TelegramConnectionLinkDto>> CreateLinkAsync(int adminUserId, CancellationToken ct = default);
    Task<ApiResponse<TelegramConnectionStatusDto>> GetStatusAsync(int adminUserId, CancellationToken ct = default);
    Task<ApiResponse<NoContentDto>> RedeemAsync(TelegramConnectionRedeemRequest request, CancellationToken ct = default);
    Task<ApiResponse<TelegramAnalyticsSubscriptionStatusDto>> SetAnalyticsSubscriptionAsync(TelegramAnalyticsSubscriptionRequest request, CancellationToken ct = default);
    Task<ApiResponse<TelegramAnalyticsSubscriptionStatusDto>> GetAnalyticsSubscriptionsAsync(long chatId, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<TelegramManagedAdminDto>>> GetManagedUsersAsync(long managerChatId, CancellationToken ct = default);
    Task<ApiResponse<TelegramConnectionLinkDto>> CreateManagedLinkAsync(long managerChatId, int adminUserId, CancellationToken ct = default);
    Task<ApiResponse<TelegramManagedAdminDto>> ClearManagedChatAsync(long managerChatId, int adminUserId, CancellationToken ct = default);
    Task<ApiResponse<TelegramManagedAdminDto>> AssignManagedChatAsync(long managerChatId, int adminUserId, long telegramChatId, CancellationToken ct = default);
    Task<ApiResponse<TelegramManagedAdminDto>> SetManagedSubscriptionAsync(long managerChatId, int adminUserId, string topic, bool enabled, CancellationToken ct = default);
}
