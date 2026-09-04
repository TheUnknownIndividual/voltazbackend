#nullable enable

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Volt.Application.Dtos.MetaInbox;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.API.Services
{
    public sealed record MetaWebhookEnqueueResult(bool Accepted, MetaInboxWebhookProcessingResult Summary);

    public sealed class MetaWebhookDurableQueue
    {
        private const int MaximumQueuedPayloads = 1_000;
        private static readonly TimeSpan PersistenceTimeout = TimeSpan.FromSeconds(8);
        private readonly DataContext _context;

        public MetaWebhookDurableQueue(DataContext context) => _context = context;

        public async Task<MetaWebhookEnqueueResult> EnqueueAsync(string payload)
        {
            var summary = Summarize(payload);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            using var timeout = new CancellationTokenSource(PersistenceTimeout);

            var existing = await _context.MetaWebhookQueueItems.AsNoTracking()
                .AnyAsync(x => x.PayloadHash == hash, timeout.Token);
            if (existing) return new MetaWebhookEnqueueResult(true, summary);

            var queuedCount = await _context.MetaWebhookQueueItems.AsNoTracking()
                .Where(x => x.Status == MetaWebhookQueueStatuses.Pending ||
                            x.Status == MetaWebhookQueueStatuses.Processing)
                .Take(MaximumQueuedPayloads)
                .CountAsync(timeout.Token);
            if (queuedCount >= MaximumQueuedPayloads)
                return new MetaWebhookEnqueueResult(false, summary);

            var now = DateTime.UtcNow;
            var item = new MetaWebhookQueueItem
            {
                PayloadHash = hash,
                Payload = payload,
                Status = MetaWebhookQueueStatuses.Pending,
                CreatedAt = now,
                NextAttemptAt = now
            };
            _context.MetaWebhookQueueItems.Add(item);
            try
            {
                await _context.SaveChangesAsync(timeout.Token);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                _context.Entry(item).State = EntityState.Detached;
            }

            return new MetaWebhookEnqueueResult(true, summary);
        }

        private static MetaInboxWebhookProcessingResult Summarize(string payload)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                var objectName = ReadString(root, "object");
                string? field = null;
                string? phoneNumberId = null;
                var messageCount = 0;
                var statusCount = 0;
                if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                    return new MetaInboxWebhookProcessingResult(objectName, null, null, 0, 0);

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var change in changes.EnumerateArray())
                    {
                        field ??= ReadString(change, "field");
                        if (!change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                            continue;
                        if (value.TryGetProperty("metadata", out var metadata))
                            phoneNumberId ??= ReadString(metadata, "phone_number_id");
                        if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                            messageCount += messages.GetArrayLength();
                        if (value.TryGetProperty("message_echoes", out var echoes) && echoes.ValueKind == JsonValueKind.Array)
                            messageCount += echoes.GetArrayLength();
                        if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                            statusCount += statuses.GetArrayLength();
                    }
                }
                return new MetaInboxWebhookProcessingResult(objectName, field, phoneNumberId, messageCount, statusCount);
            }
            catch (JsonException)
            {
                return new MetaInboxWebhookProcessingResult(null, null, null, 0, 0);
            }
        }

        private static string? ReadString(JsonElement value, string property)
            => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) &&
               item.ValueKind == JsonValueKind.String
                ? item.GetString()
                : null;

        private static bool IsUniqueViolation(DbUpdateException exception)
            => exception.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
