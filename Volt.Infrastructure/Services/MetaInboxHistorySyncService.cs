#nullable enable

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Dtos.MetaInbox;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services
{
    public sealed class MetaInboxHistorySyncService : IMetaInboxHistorySyncService
    {
        private static readonly TimeSpan AbandonedRequestThreshold = TimeSpan.FromMinutes(2);
        private static readonly string[] RequiredWebhookFields =
        {
            "history",
            "smb_app_state_sync",
            "smb_message_echoes"
        };
        private readonly DataContext _context;
        private readonly HttpClient _client;
        private readonly MetaInboxOptions _options;
        private readonly IAdminAuditService _audit;

        public MetaInboxHistorySyncService(
            DataContext context,
            HttpClient client,
            IOptions<MetaInboxOptions> options,
            IAdminAuditService audit)
        {
            _context = context;
            _client = client;
            _options = options.Value;
            _audit = audit;
        }

        public async Task<MetaInboxHistorySyncDto?> GetStatusAsync(string phoneNumberId, CancellationToken ct = default)
        {
            var normalizedPhoneId = NormalizePhoneId(phoneNumberId);
            if (normalizedPhoneId is null) return null;
            var record = await _context.MetaInboxHistorySyncs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PhoneNumberId == normalizedPhoneId, ct);
            return record is null ? NotRequested(normalizedPhoneId) : Map(record);
        }

        public async Task<ApiResponse<MetaInboxHistorySyncDto>> RequestAsync(
            string phoneNumberId,
            int actorAdminUserId,
            CancellationToken ct = default)
        {
            var normalizedPhoneId = NormalizePhoneId(phoneNumberId);
            if (normalizedPhoneId is null ||
                !string.Equals(normalizedPhoneId, _options.WhatsAppPhoneNumberId?.Trim(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(_options.WhatsAppAccessToken))
            {
                return Error("WHATSAPP_HISTORY_NOT_CONFIGURED", "WhatsApp history synchronization is not configured for this phone number.");
            }

            var record = await _context.MetaInboxHistorySyncs
                .FirstOrDefaultAsync(x => x.PhoneNumberId == normalizedPhoneId, ct);
            if (record is not null && record.Status is MetaInboxHistorySyncStatuses.Requested or MetaInboxHistorySyncStatuses.Processing or MetaInboxHistorySyncStatuses.Completed or MetaInboxHistorySyncStatuses.Declined)
            {
                if (record.MetaRequestId is not null || record.Status != MetaInboxHistorySyncStatuses.Requested ||
                    record.UpdatedAt >= DateTime.UtcNow.Subtract(AbandonedRequestThreshold))
                    return ApiResponse<MetaInboxHistorySyncDto>.SuccessResponse(Map(record));
            }

            var now = DateTime.UtcNow;
            if (record is null)
            {
                record = new MetaInboxHistorySync
                {
                    PhoneNumberId = normalizedPhoneId,
                    Status = MetaInboxHistorySyncStatuses.Requested,
                    RequestedAt = now,
                    UpdatedAt = now
                };
                _context.MetaInboxHistorySyncs.Add(record);
                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    _context.Entry(record).State = EntityState.Detached;
                    record = await _context.MetaInboxHistorySyncs
                        .FirstAsync(x => x.PhoneNumberId == normalizedPhoneId, ct);
                    return ApiResponse<MetaInboxHistorySyncDto>.SuccessResponse(Map(record));
                }
            }
            else
            {
                record.Status = MetaInboxHistorySyncStatuses.Requested;
                record.MetaRequestId = null;
                record.Progress = 0;
                record.Phase = null;
                record.LastChunkOrder = null;
                record.RequestedAt = now;
                record.CompletedAt = null;
                record.UpdatedAt = now;
                record.ErrorCode = null;
                record.ErrorMessage = null;
                await _context.SaveChangesAsync(ct);
            }

            try
            {
                var subscribedWebhookFields = await GetWhatsAppWebhookFieldsAsync(ct);
                var missingWebhookFields = RequiredWebhookFields
                    .Where(field => !subscribedWebhookFields.Contains(field))
                    .ToArray();
                if (missingWebhookFields.Length > 0)
                {
                    return await FailRequestAsync(
                        record,
                        "WHATSAPP_HISTORY_WEBHOOK_MISSING",
                        $"Subscribe these Meta WhatsApp webhook fields before requesting the one-time import: {string.Join(", ", missingWebhookFields)}.",
                        actorAdminUserId,
                        ct);
                }

                await TryRequestContactSyncAsync(normalizedPhoneId, ct);
                using var response = await SendSyncRequestAsync(normalizedPhoneId, "history", ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    return await FailRequestAsync(
                        record,
                        $"META_HTTP_{(int)response.StatusCode}",
                        HistoryRequestFailureMessage(response.StatusCode),
                        actorAdminUserId,
                        ct);
                }

                string? requestId = null;
                try
                {
                    using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                    requestId = ReadString(document.RootElement, "request_id");
                }
                catch (JsonException)
                {
                    return await FailRequestAsync(
                        record,
                        "META_INVALID_RESPONSE",
                        "Meta accepted the request but returned an invalid response. Check the connection and retry promptly.",
                        actorAdminUserId,
                        ct);
                }

                if (string.IsNullOrWhiteSpace(requestId))
                {
                    return await FailRequestAsync(
                        record,
                        "META_REQUEST_ID_MISSING",
                        "Meta did not return a history synchronization request ID.",
                        actorAdminUserId,
                        ct);
                }

                record.MetaRequestId = Trim(requestId, 256);
                record.Status = MetaInboxHistorySyncStatuses.Requested;
                record.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_HISTORY_SYNC_REQUESTED", normalizedPhoneId, true, ct);
                return ApiResponse<MetaInboxHistorySyncDto>.SuccessResponse(Map(record));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return await FailRequestAsync(
                    record,
                    "META_HISTORY_REQUEST_FAILED",
                    "The history request could not reach Meta. Retry while the 24-hour onboarding window is still open.",
                    actorAdminUserId,
                    ct);
            }
        }

        public async Task<MetaInboxHistorySyncDto> RecordProgressAsync(
            MetaInboxHistorySyncProgressUpdate update,
            CancellationToken ct = default)
        {
            var phoneNumberId = NormalizePhoneId(update.PhoneNumberId) ?? throw new ArgumentException("Invalid phone number ID.");
            var record = await _context.MetaInboxHistorySyncs.FirstOrDefaultAsync(x => x.PhoneNumberId == phoneNumberId, ct);
            var progress = Math.Clamp(update.Progress, 0, 100);
            var phase = update.Phase is >= 0 and <= 2 ? update.Phase : null;
            var chunkOrder = update.ChunkOrder is >= 0 ? update.ChunkOrder : null;
            if (record is null)
            {
                record = new MetaInboxHistorySync
                {
                    PhoneNumberId = phoneNumberId,
                    Status = progress >= 100 ? MetaInboxHistorySyncStatuses.Completed : MetaInboxHistorySyncStatuses.Processing,
                    Progress = progress,
                    Phase = phase,
                    LastChunkOrder = chunkOrder,
                    RequestedAt = update.ObservedAt,
                    CompletedAt = progress >= 100 ? update.ObservedAt : null,
                    UpdatedAt = update.ObservedAt
                };
                _context.MetaInboxHistorySyncs.Add(record);
                try
                {
                    await _context.SaveChangesAsync(ct);
                    return Map(record);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    _context.Entry(record).State = EntityState.Detached;
                }
            }

            var progressQuery = _context.MetaInboxHistorySyncs
                .Where(x => x.PhoneNumberId == phoneNumberId &&
                            x.Status != MetaInboxHistorySyncStatuses.Completed &&
                            x.Status != MetaInboxHistorySyncStatuses.Declined &&
                            x.Progress <= progress);
            if (progress >= 100)
            {
                await progressQuery.ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.Progress, progress)
                    .SetProperty(x => x.Phase, x => phase ?? x.Phase)
                    .SetProperty(x => x.LastChunkOrder, x => chunkOrder ?? x.LastChunkOrder)
                    .SetProperty(x => x.Status, MetaInboxHistorySyncStatuses.Completed)
                    .SetProperty(x => x.CompletedAt, update.ObservedAt)
                    .SetProperty(x => x.UpdatedAt, update.ObservedAt)
                    .SetProperty(x => x.ErrorCode, (string?)null)
                    .SetProperty(x => x.ErrorMessage, (string?)null), ct);
            }
            else
            {
                await progressQuery.ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.Progress, progress)
                    .SetProperty(x => x.Phase, x => phase ?? x.Phase)
                    .SetProperty(x => x.LastChunkOrder, x => chunkOrder ?? x.LastChunkOrder)
                    .SetProperty(x => x.Status, MetaInboxHistorySyncStatuses.Processing)
                    .SetProperty(x => x.UpdatedAt, update.ObservedAt)
                    .SetProperty(x => x.ErrorCode, (string?)null)
                    .SetProperty(x => x.ErrorMessage, (string?)null), ct);
            }

            return Map(await _context.MetaInboxHistorySyncs.AsNoTracking()
                .FirstAsync(x => x.PhoneNumberId == phoneNumberId, ct));
        }

        public async Task<MetaInboxHistorySyncDto> RecordFailureAsync(
            MetaInboxHistorySyncFailureUpdate update,
            CancellationToken ct = default)
        {
            var phoneNumberId = NormalizePhoneId(update.PhoneNumberId) ?? throw new ArgumentException("Invalid phone number ID.");
            var record = await _context.MetaInboxHistorySyncs.FirstOrDefaultAsync(x => x.PhoneNumberId == phoneNumberId, ct);
            if (record is null)
            {
                record = new MetaInboxHistorySync
                {
                    PhoneNumberId = phoneNumberId,
                    RequestedAt = update.ObservedAt
                };
                _context.MetaInboxHistorySyncs.Add(record);
            }
            else if (record.Status == MetaInboxHistorySyncStatuses.Completed)
            {
                return Map(record);
            }
            var failureStatus = update.Status == MetaInboxHistorySyncStatuses.Declined
                ? MetaInboxHistorySyncStatuses.Declined
                : MetaInboxHistorySyncStatuses.Failed;
            if (record.Id == 0)
            {
                record.Status = failureStatus;
                record.ErrorCode = Trim(update.ErrorCode, 100);
                record.ErrorMessage = Trim(update.ErrorMessage, 1000);
                record.UpdatedAt = update.ObservedAt;
                record.CompletedAt = update.ObservedAt;
                try
                {
                    await _context.SaveChangesAsync(ct);
                    return Map(record);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    _context.Entry(record).State = EntityState.Detached;
                }
            }

            await _context.MetaInboxHistorySyncs
                .Where(x => x.PhoneNumberId == phoneNumberId && x.Status != MetaInboxHistorySyncStatuses.Completed)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(x => x.Status, failureStatus)
                    .SetProperty(x => x.ErrorCode, Trim(update.ErrorCode, 100))
                    .SetProperty(x => x.ErrorMessage, Trim(update.ErrorMessage, 1000))
                    .SetProperty(x => x.UpdatedAt, update.ObservedAt)
                    .SetProperty(x => x.CompletedAt, update.ObservedAt), ct);
            return Map(await _context.MetaInboxHistorySyncs.AsNoTracking()
                .FirstAsync(x => x.PhoneNumberId == phoneNumberId, ct));
        }

        private async Task<HashSet<string>> GetWhatsAppWebhookFieldsAsync(CancellationToken ct)
        {
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(_options.AppId) || string.IsNullOrWhiteSpace(_options.AppSecret)) return fields;
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{Version()}/{Uri.EscapeDataString(_options.AppId)}/subscriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{_options.AppId}|{_options.AppSecret}");
            using var response = await _client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return fields;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return fields;
            foreach (var subscription in data.EnumerateArray())
            {
                if (!string.Equals(ReadString(subscription, "object"), "whatsapp_business_account", StringComparison.OrdinalIgnoreCase) ||
                    !subscription.TryGetProperty("fields", out var subscribedFields) || subscribedFields.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var field in subscribedFields.EnumerateArray())
                {
                    var name = field.ValueKind == JsonValueKind.String ? field.GetString() : ReadString(field, "name");
                    if (!string.IsNullOrWhiteSpace(name)) fields.Add(name);
                }
            }
            return fields;
        }

        private async Task TryRequestContactSyncAsync(string phoneNumberId, CancellationToken ct)
        {
            try
            {
                using var response = await SendSyncRequestAsync(phoneNumberId, "smb_app_state_sync", ct);
                _ = response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // A contact-sync timeout must not consume the short history-sync window.
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _ = exception;
                // Contact names improve the import but must never prevent the time-sensitive history request.
            }
        }

        private async Task<HttpResponseMessage> SendSyncRequestAsync(string phoneNumberId, string syncType, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{Version()}/{Uri.EscapeDataString(phoneNumberId)}/smb_app_data")
            {
                Content = JsonContent.Create(new { messaging_product = "whatsapp", sync_type = syncType })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.WhatsAppAccessToken);
            return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        private async Task<ApiResponse<MetaInboxHistorySyncDto>> FailRequestAsync(
            MetaInboxHistorySync record,
            string code,
            string message,
            int actorAdminUserId,
            CancellationToken ct)
        {
            record.Status = MetaInboxHistorySyncStatuses.Failed;
            record.ErrorCode = Trim(code, 100);
            record.ErrorMessage = Trim(message, 1000);
            record.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_HISTORY_SYNC_FAILED", record.PhoneNumberId, false, ct);
            return Error(code, message);
        }

        private async Task WriteAuditSafelyAsync(int actorAdminUserId, string action, string phoneNumberId, bool succeeded, CancellationToken ct)
        {
            try
            {
                await _audit.WriteAsync(actorAdminUserId, null, action, "WhatsAppHistorySync", phoneNumberId, null, succeeded, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _ = exception;
                // Auditing must not make a Meta request appear to fail after it was accepted.
            }
        }

        private string Version()
        {
            var version = (_options.GraphApiVersion ?? string.Empty).Trim().Trim('/');
            return version.StartsWith('v') && version.Skip(1).All(character => char.IsDigit(character) || character == '.')
                ? version
                : "v26.0";
        }

        private static string HistoryRequestFailureMessage(HttpStatusCode statusCode) => statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Meta rejected the server token for history synchronization.",
            HttpStatusCode.BadRequest => "Meta rejected the history request. The one-time request may already have been used or the 24-hour onboarding window may have expired.",
            _ => $"Meta rejected the history request with HTTP {(int)statusCode}."
        };

        private static string? NormalizePhoneId(string? value)
        {
            var normalized = value?.Trim();
            return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 128 && normalized.All(char.IsDigit)
                ? normalized
                : null;
        }

        private static string? ReadString(JsonElement element, string propertyName)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static string Trim(string value, int max)
        {
            var normalized = (value ?? string.Empty).Trim();
            return normalized.Length <= max ? normalized : normalized[..max];
        }

        private static bool IsUniqueViolation(DbUpdateException exception)
            => exception.InnerException is SqlException { Number: 2601 or 2627 };

        private static MetaInboxHistorySyncDto Map(MetaInboxHistorySync record) => new(
            record.PhoneNumberId,
            record.MetaRequestId,
            record.Status,
            record.Progress,
            record.Phase,
            record.LastChunkOrder,
            record.RequestedAt,
            record.CompletedAt,
            record.UpdatedAt,
            record.ErrorCode,
            record.ErrorMessage);

        private static MetaInboxHistorySyncDto NotRequested(string phoneNumberId) => new(
            phoneNumberId,
            null,
            "not_requested",
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        private static ApiResponse<MetaInboxHistorySyncDto> Error(string code, string details)
            => ApiResponse<MetaInboxHistorySyncDto>.ErrorResponse(code, details);
    }
}
