using System.Threading.Channels;
using Volt.Application.Dtos.Seo;
using Volt.Application.Interfaces;

namespace Volt.API.Services
{
    public sealed class SeoSubmissionQueue : ISeoSubmissionQueue
    {
        private readonly Channel<SeoProductCreatedNotification> _queue =
            Channel.CreateUnbounded<SeoProductCreatedNotification>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        public void EnqueueProductCreated(int productId)
        {
            if (productId <= 0)
            {
                return;
            }

            _queue.Writer.TryWrite(new SeoProductCreatedNotification(productId));
        }

        public IAsyncEnumerable<SeoProductCreatedNotification> DequeueAllAsync(CancellationToken ct = default)
            => _queue.Reader.ReadAllAsync(ct);
    }
}
