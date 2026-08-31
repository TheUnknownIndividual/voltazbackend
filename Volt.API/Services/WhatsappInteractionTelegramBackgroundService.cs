using Microsoft.EntityFrameworkCore;
using Volt.Application.Dtos.SolarAnalytics;
using Volt.Application.Interfaces;
using Volt.Infrastructure.Data;

namespace Volt.API.Services;

public sealed class WhatsappInteractionTelegramBackgroundService : BackgroundService
{
    private readonly IWhatsappInteractionNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsappInteractionTelegramBackgroundService> _logger;

    public WhatsappInteractionTelegramBackgroundService(
        IWhatsappInteractionNotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsappInteractionTelegramBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(notification, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "WhatsApp interaction Telegram dispatch failed. Topic={Topic} ExceptionType={ExceptionType}",
                    notification.Topic,
                    exception.GetType().Name);
            }
        }
    }

    private async Task DeliverAsync(WhatsappInteractionNotification notification, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var recipientsQuery = context.AdminUsers
            .AsNoTracking()
            .Where(x => x.IsActive && x.TelegramChatId.HasValue);

        recipientsQuery = notification.Topic switch
        {
            WhatsappNotificationTopics.Yoxla => recipientsQuery.Where(x => x.ReceivesYoxlaNotifications),
            WhatsappNotificationTopics.Qiymetlendirme => recipientsQuery.Where(x => x.ReceivesQiymetlendirmeNotifications),
            _ => recipientsQuery.Where(_ => false)
        };

        var chatIds = await recipientsQuery
            .Select(x => x.TelegramChatId!.Value)
            .Distinct()
            .ToListAsync(ct);
        if (chatIds.Count == 0) return;

        var telegram = scope.ServiceProvider.GetRequiredService<ITelegramTaskNotificationService>();
        await Parallel.ForEachAsync(
            chatIds,
            new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = 4 },
            async (chatId, token) =>
            {
                var status = await telegram.SendWhatsappInteractionAsync(chatId, notification, token);
                if (!string.Equals(status, "Delivered", StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "WhatsApp interaction Telegram alert was not delivered. Topic={Topic} Status={Status}",
                        notification.Topic,
                        status);
                }
            });
    }
}
