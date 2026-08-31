using System.Threading.Channels;
using Volt.Application.Dtos.SolarAnalytics;
using Volt.Application.Interfaces;

namespace Volt.API.Services;

/// <summary>
/// Keeps Telegram delivery off the public analytics request path. The bounded
/// channel prevents an external integration outage from growing memory usage.
/// The database analytics row remains the source of truth if the queue is full.
/// </summary>
public sealed class WhatsappInteractionNotificationQueue : IWhatsappInteractionNotificationQueue
{
    private readonly Channel<WhatsappInteractionNotification> _channel = Channel.CreateBounded<WhatsappInteractionNotification>(
        new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool TryEnqueue(WhatsappInteractionNotification notification)
        => _channel.Writer.TryWrite(notification);

    public IAsyncEnumerable<WhatsappInteractionNotification> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
}
