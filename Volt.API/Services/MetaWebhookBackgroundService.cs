#nullable enable

using Microsoft.EntityFrameworkCore;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.API.Services
{
    public sealed class MetaWebhookBackgroundService : BackgroundService
    {
        private const int MaximumAttempts = 5;
        private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MetaWebhookBackgroundService> _logger;
        private DateTime _nextCleanupAt = DateTime.UtcNow;

        public MetaWebhookBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<MetaWebhookBackgroundService> logger)
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
                    var processed = await ProcessOneAsync(stoppingToken);
                    if (!processed) await Task.Delay(IdleDelay, stoppingToken);
                    if (DateTime.UtcNow >= _nextCleanupAt)
                        await CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "The durable Meta webhook worker failed; queued payloads remain available for retry.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private async Task<bool> ProcessOneAsync(CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var service = scope.ServiceProvider.GetRequiredService<IMetaInboxService>();
            var now = DateTime.UtcNow;
            var candidateId = await context.MetaWebhookQueueItems.AsNoTracking()
                .Where(x => x.Payload != null && x.Attempts < MaximumAttempts && x.NextAttemptAt <= now &&
                            (x.Status == MetaWebhookQueueStatuses.Pending ||
                             (x.Status == MetaWebhookQueueStatuses.Processing && x.LeaseUntil < now)))
                .OrderBy(x => x.CreatedAt)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(ct);
            if (!candidateId.HasValue) return false;

            var leaseId = Guid.NewGuid();
            var leaseUntil = now.Add(LeaseDuration);
            var claimed = await context.MetaWebhookQueueItems
                .Where(x => x.Id == candidateId.Value && x.Payload != null && x.Attempts < MaximumAttempts &&
                            x.NextAttemptAt <= now &&
                            (x.Status == MetaWebhookQueueStatuses.Pending ||
                             (x.Status == MetaWebhookQueueStatuses.Processing && x.LeaseUntil < now)))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.Status, MetaWebhookQueueStatuses.Processing)
                    .SetProperty(x => x.LeaseId, leaseId)
                    .SetProperty(x => x.LeaseUntil, leaseUntil)
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
            if (claimed != 1) return true;

            var item = await context.MetaWebhookQueueItems
                .FirstAsync(x => x.Id == candidateId.Value && x.LeaseId == leaseId, ct);
            try
            {
                await service.ProcessWebhookAsync(item.Payload!, ct);
                item.Status = MetaWebhookQueueStatuses.Completed;
                item.Payload = null;
                item.CompletedAt = DateTime.UtcNow;
                item.NextAttemptAt = item.CompletedAt.Value;
                item.LeaseId = null;
                item.LeaseUntil = null;
                item.LastError = null;
                await context.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var permanentFailure = item.Attempts >= MaximumAttempts;
                item.Status = permanentFailure ? MetaWebhookQueueStatuses.Failed : MetaWebhookQueueStatuses.Pending;
                item.NextAttemptAt = DateTime.UtcNow.Add(RetryDelay(item.Attempts));
                item.LeaseId = null;
                item.LeaseUntil = null;
                item.LastError = SafeError(exception);
                if (permanentFailure)
                {
                    // Retain only operational metadata after retries; do not keep message contents indefinitely.
                    item.Payload = null;
                    item.CompletedAt = DateTime.UtcNow;
                }
                await context.SaveChangesAsync(ct);
                _logger.LogWarning(
                    "Meta webhook queue item {QueueItemId} failed on attempt {Attempt}; retry scheduled: {WillRetry}.",
                    item.Id,
                    item.Attempts,
                    !permanentFailure);
            }
            return true;
        }

        private async Task CleanupAsync(CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var cutoff = DateTime.UtcNow.Subtract(Retention);
            await context.MetaWebhookQueueItems
                .Where(x => x.Payload == null && x.CompletedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            _nextCleanupAt = DateTime.UtcNow.AddHours(1);
        }

        private static TimeSpan RetryDelay(int attempts) => attempts switch
        {
            <= 1 => TimeSpan.FromSeconds(10),
            2 => TimeSpan.FromMinutes(1),
            3 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(15)
        };

        private static string SafeError(Exception exception)
        {
            var value = exception.GetType().Name;
            return value.Length <= 500 ? value : value[..500];
        }
    }
}
