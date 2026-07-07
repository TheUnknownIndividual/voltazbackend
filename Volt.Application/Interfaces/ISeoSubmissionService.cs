using Volt.Application.Dtos.Seo;

namespace Volt.Application.Interfaces
{
    public interface ISeoSubmissionService
    {
        Task SubmitProductCreatedAsync(SeoProductCreatedNotification notification, CancellationToken ct = default);
    }
}
