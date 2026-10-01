namespace Volt.API.Services
{
    // Best-effort housekeeping: keeps the LinkedIn access token fresh so it's never close to expiry.
    // LinkedInSocialPublisher also refreshes inline before every publish, so a missed tick here never
    // blocks a post -- this just avoids relying on that happening to coincide with a post.
    public sealed class LinkedInTokenRefreshBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LinkedInTokenRefreshBackgroundService> _logger;

        public LinkedInTokenRefreshBackgroundService(
            IServiceScopeFactory scopeFactory, ILogger<LinkedInTokenRefreshBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<LinkedInSocialPublisher>();
                    await publisher.RefreshIfNeededAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LinkedIn token refresh tick failed.");
                }

                try { await Task.Delay(TimeSpan.FromHours(12), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
