using Volt.Application.Dtos.Seo;

namespace Volt.Application.Interfaces
{
    public interface ISeoSubmissionQueue
    {
        void EnqueueProductCreated(int productId);
        IAsyncEnumerable<SeoProductCreatedNotification> DequeueAllAsync(CancellationToken ct = default);
    }
}
