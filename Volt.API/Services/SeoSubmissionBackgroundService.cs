using Volt.Application.Interfaces;

namespace Volt.API.Services
{
    public sealed class SeoSubmissionBackgroundService : BackgroundService
    {
        private readonly ISeoSubmissionQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SeoSubmissionBackgroundService> _logger;

        public SeoSubmissionBackgroundService(
            ISeoSubmissionQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<SeoSubmissionBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var notification in _queue.DequeueAllAsync(stoppingToken))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var service = scope.ServiceProvider.GetRequiredService<ISeoSubmissionService>();
                        await service.SubmitProductCreatedAsync(notification, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SEO submission failed for product {ProductId}.", notification.ProductId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}
