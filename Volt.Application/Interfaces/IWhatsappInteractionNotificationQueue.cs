using Volt.Application.Dtos.SolarAnalytics;

namespace Volt.Application.Interfaces;

public interface IWhatsappInteractionNotificationQueue
{
    bool TryEnqueue(WhatsappInteractionNotification notification);
    IAsyncEnumerable<WhatsappInteractionNotification> ReadAllAsync(CancellationToken ct = default);
}
