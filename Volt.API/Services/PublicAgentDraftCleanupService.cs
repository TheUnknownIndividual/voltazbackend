using Microsoft.EntityFrameworkCore;
using Volt.Infrastructure.Data;

namespace Volt.API.Services;

public sealed class PublicAgentDraftCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PublicAgentDraftCleanupService> _logger;

    public PublicAgentDraftCleanupService(IServiceScopeFactory scopeFactory, ILogger<PublicAgentDraftCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                var now = DateTime.UtcNow;
                await db.PublicAgentContactDrafts
                    .Where(x => x.Status == "PendingConfirmation" && x.ExpiresAt <= now)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Expired"), stoppingToken);
                await db.PublicAgentContactDrafts
                    .Where(x => x.ExpiresAt <= now.AddDays(-1))
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception error)
            {
                _logger.LogWarning(error, "Public agent contact draft cleanup failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
