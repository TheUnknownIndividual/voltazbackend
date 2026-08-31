using Volt.Application.Dtos.AdminProjectTracker;
using Volt.Application.Dtos.SolarAnalytics;

namespace Volt.Application.Interfaces;

public interface ITelegramTaskNotificationService
{
    /// <returns>A safe delivery status for persistence; it must not include token or API response content.</returns>
    Task<string> SendTaskAssignedAsync(long chatId, string projectName, string taskTitle, string description, DateTime? dueAt, CancellationToken ct = default);
    Task<string> SendProjectApprovedAsync(long chatId, string projectName, CancellationToken ct = default);
    Task<string> SendProjectApprovalRequestAsync(long chatId, int requestId, string environmentScope, StakeholderApprovalNotification project, bool useRussian, CancellationToken ct = default);
    Task<string> SendWhatsappInteractionAsync(long chatId, WhatsappInteractionNotification notification, CancellationToken ct = default);
}
