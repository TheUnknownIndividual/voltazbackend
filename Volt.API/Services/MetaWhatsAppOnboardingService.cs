#nullable enable

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Dtos.MetaInbox;
using Volt.Application.Interfaces;
using Volt.Domain.Common;

namespace Volt.API.Services
{
    public sealed class MetaWhatsAppOnboardingService : IMetaWhatsAppOnboardingService
    {
        private static readonly Regex PinPattern = new("^[0-9]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly HttpClient _client;
        private readonly MetaInboxOptions _options;
        private readonly MetaWhatsAppOnboardingSessionStore _sessions;
        private readonly IAdminAuditService _audit;
        private readonly IMetaInboxHistorySyncService _historySync;
        private readonly ILogger<MetaWhatsAppOnboardingService> _logger;

        public MetaWhatsAppOnboardingService(
            HttpClient client,
            IOptions<MetaInboxOptions> options,
            MetaWhatsAppOnboardingSessionStore sessions,
            IAdminAuditService audit,
            IMetaInboxHistorySyncService historySync,
            ILogger<MetaWhatsAppOnboardingService> logger)
        {
            _client = client;
            _options = options.Value;
            _sessions = sessions;
            _audit = audit;
            _historySync = historySync;
            _logger = logger;
        }

        public async Task<WhatsAppOnboardingStatusDto> GetStatusAsync(CancellationToken ct = default)
        {
            if (!IsConfigured()) return EmptyStatus("WhatsApp Embedded Signup is not fully configured on the server.");
            try
            {
                return await FetchStatusAsync(_options.WhatsAppAccessToken, ct);
            }
            catch (MetaGraphException exception)
            {
                _logger.LogWarning("WhatsApp onboarding status check failed with Meta HTTP {StatusCode}.", exception.StatusCode);
                return EmptyStatus($"Meta status request failed with HTTP {(int)exception.StatusCode}.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "WhatsApp onboarding status check failed.");
                return EmptyStatus("WhatsApp connection status could not be loaded.");
            }
        }

        public async Task<ApiResponse<WhatsAppOnboardingResultDto>> CompleteAsync(
            WhatsAppOnboardingCompleteRequest request,
            int actorAdminUserId,
            CancellationToken ct = default)
        {
            if (!IsConfigured()) return Error("WhatsApp Embedded Signup is not fully configured on the server.");
            var code = request.Code?.Trim() ?? string.Empty;
            var wabaId = request.WhatsAppBusinessAccountId?.Trim() ?? string.Empty;
            var phoneNumberId = request.PhoneNumberId?.Trim() ?? string.Empty;
            if (code.Length is < 8 or > 4096 || !IsNumericId(wabaId) || !IsNumericId(phoneNumberId))
                return Error("Meta returned incomplete or invalid onboarding information.");
            if (!FixedEquals(wabaId, _options.WhatsAppBusinessAccountId) || !FixedEquals(phoneNumberId, _options.WhatsAppPhoneNumberId))
                return Error("The selected WhatsApp account or phone number does not match the Volt production account.");

            try
            {
                var temporaryToken = await ExchangeCodeAsync(code, ct);
                await VerifySelectedAssetsAsync(temporaryToken, wabaId, phoneNumberId, ct);
                await SubscribeAppAsync(temporaryToken, wabaId, ct);
                var temporaryStatus = await FetchStatusAsync(temporaryToken, ct);

                if (temporaryStatus.Connected)
                {
                    var permanentStatus = await FetchStatusAsync(_options.WhatsAppAccessToken, ct);
                    if (permanentStatus.Connected && permanentStatus.WebhookHistorySubscribed)
                        await TryRequestHistorySyncAsync(actorAdminUserId, ct);
                    await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_ONBOARDING_COMPLETED", phoneNumberId, true, ct);
                    return ApiResponse<WhatsAppOnboardingResultDto>.SuccessResponse(
                        new WhatsAppOnboardingResultDto(permanentStatus.Connected, false, null, permanentStatus));
                }

                var continuationToken = _sessions.Create(temporaryToken, wabaId, phoneNumberId);
                await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_ONBOARDING_PIN_REQUIRED", phoneNumberId, true, ct);
                return ApiResponse<WhatsAppOnboardingResultDto>.SuccessResponse(
                    new WhatsAppOnboardingResultDto(false, true, continuationToken, temporaryStatus));
            }
            catch (MetaGraphException exception)
            {
                _logger.LogWarning("WhatsApp onboarding completion failed with Meta HTTP {StatusCode}.", exception.StatusCode);
                await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_ONBOARDING_FAILED", phoneNumberId, false, ct);
                return Error($"Meta rejected the onboarding request with HTTP {(int)exception.StatusCode}.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "WhatsApp onboarding completion failed.");
                await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_ONBOARDING_FAILED", phoneNumberId, false, ct);
                return Error("WhatsApp onboarding could not be completed.");
            }
        }

        public async Task<ApiResponse<WhatsAppOnboardingResultDto>> RegisterAsync(
            WhatsAppOnboardingRegisterRequest request,
            int actorAdminUserId,
            CancellationToken ct = default)
        {
            var continuationToken = request.ContinuationToken?.Trim() ?? string.Empty;
            var pin = request.Pin?.Trim() ?? string.Empty;
            if (!PinPattern.IsMatch(pin)) return Error("Enter a valid six-digit WhatsApp two-step verification PIN.");
            if (!_sessions.TryGet(continuationToken, out var session) || session is null)
                return Error("The secure onboarding session expired. Start WhatsApp connection again.");

            try
            {
                var content = JsonContent.Create(new { messaging_product = "whatsapp", pin });
                await SendGraphAsync(HttpMethod.Post, $"{Version()}/{Uri.EscapeDataString(session.PhoneNumberId)}/register", session.AccessToken, content, ct);
                await SubscribeAppAsync(session.AccessToken, session.WhatsAppBusinessAccountId, ct);
                _sessions.Remove(continuationToken);

                var permanentStatus = await FetchStatusAsync(_options.WhatsAppAccessToken, ct);
                if (permanentStatus.Connected && permanentStatus.WebhookHistorySubscribed)
                    await TryRequestHistorySyncAsync(actorAdminUserId, ct);
                await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_PHONE_REGISTERED", session.PhoneNumberId, permanentStatus.Connected, ct);
                return ApiResponse<WhatsAppOnboardingResultDto>.SuccessResponse(
                    new WhatsAppOnboardingResultDto(permanentStatus.Connected, false, null, permanentStatus));
            }
            catch (MetaGraphException exception)
            {
                _logger.LogWarning("WhatsApp phone registration failed with Meta HTTP {StatusCode}.", exception.StatusCode);
                await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_PHONE_REGISTRATION_FAILED", session.PhoneNumberId, false, ct);
                return Error($"Meta rejected phone registration with HTTP {(int)exception.StatusCode}. Check the PIN and try again.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "WhatsApp phone registration failed.");
                await WriteAuditSafelyAsync(actorAdminUserId, "WHATSAPP_PHONE_REGISTRATION_FAILED", session.PhoneNumberId, false, ct);
                return Error("WhatsApp phone registration could not be completed.");
            }
        }

        private async Task<string> ExchangeCodeAsync(string code, CancellationToken ct)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.AppId,
                ["client_secret"] = _options.AppSecret,
                ["code"] = code
            });
            var root = await SendGraphAsync(HttpMethod.Post, $"{Version()}/oauth/access_token", null, content, ct);
            var token = ReadString(root, "access_token");
            if (string.IsNullOrWhiteSpace(token)) throw new MetaGraphException(HttpStatusCode.BadGateway);
            return token;
        }

        private async Task VerifySelectedAssetsAsync(string accessToken, string wabaId, string phoneNumberId, CancellationToken ct)
        {
            var waba = await SendGraphAsync(HttpMethod.Get, $"{Version()}/{Uri.EscapeDataString(wabaId)}?fields=id,name", accessToken, null, ct);
            if (!FixedEquals(ReadString(waba, "id"), wabaId)) throw new MetaGraphException(HttpStatusCode.Forbidden);

            var phones = await SendGraphAsync(HttpMethod.Get,
                $"{Version()}/{Uri.EscapeDataString(wabaId)}/phone_numbers?fields=id&limit=100", accessToken, null, ct);
            if (!phones.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array ||
                !data.EnumerateArray().Any(item => FixedEquals(ReadString(item, "id"), phoneNumberId)))
                throw new MetaGraphException(HttpStatusCode.Forbidden);
        }

        private async Task SubscribeAppAsync(string accessToken, string wabaId, CancellationToken ct)
        {
            await SendGraphAsync(HttpMethod.Post,
                $"{Version()}/{Uri.EscapeDataString(wabaId)}/subscribed_apps",
                accessToken,
                JsonContent.Create(new { }),
                ct);
        }

        private async Task<WhatsAppOnboardingStatusDto> FetchStatusAsync(string accessToken, CancellationToken ct)
        {
            var phoneId = Uri.EscapeDataString(_options.WhatsAppPhoneNumberId);
            var phone = await SendGraphAsync(HttpMethod.Get,
                $"{Version()}/{phoneId}?fields=id,display_phone_number,verified_name,quality_rating,code_verification_status,status,platform_type,account_mode",
                accessToken,
                null,
                ct);

            var appSubscribed = false;
            var webhookFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var statusWarnings = new List<string>();
            try { appSubscribed = await IsAppSubscribedAsync(accessToken, ct); }
            catch (MetaGraphException exception)
            {
                _logger.LogWarning("Could not verify the WABA app subscription with Meta HTTP {StatusCode}.", exception.StatusCode);
                statusWarnings.Add("WABA app subscription could not be verified.");
            }
            try { webhookFields = await GetWhatsAppWebhookFieldsAsync(ct); }
            catch (MetaGraphException exception)
            {
                _logger.LogWarning("Could not verify the WhatsApp messages webhook subscription with Meta HTTP {StatusCode}.", exception.StatusCode);
                statusWarnings.Add("The messages webhook subscription could not be verified.");
            }
            var runtimeStatus = ReadString(phone, "status");
            var platformType = ReadString(phone, "platform_type");
            var connected = string.Equals(runtimeStatus, "CONNECTED", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(platformType, "CLOUD_API", StringComparison.OrdinalIgnoreCase);

            return new WhatsAppOnboardingStatusDto(
                true,
                connected,
                _options.AppId,
                _options.WhatsAppEmbeddedSignupConfigurationId,
                _options.WhatsAppOAuthRedirectUri,
                _options.WhatsAppBusinessAccountId,
                _options.WhatsAppPhoneNumberId,
                ReadString(phone, "display_phone_number"),
                ReadString(phone, "verified_name"),
                ReadString(phone, "quality_rating"),
                ReadString(phone, "code_verification_status"),
                runtimeStatus,
                platformType,
                ReadString(phone, "account_mode"),
                appSubscribed,
                webhookFields.Contains("messages"),
                webhookFields.Contains("history"),
                webhookFields.Contains("smb_message_echoes"),
                webhookFields.Contains("smb_app_state_sync"),
                statusWarnings.Count == 0 ? null : string.Join(" ", statusWarnings));
        }

        private async Task<bool> IsAppSubscribedAsync(string accessToken, CancellationToken ct)
        {
            var root = await SendGraphAsync(HttpMethod.Get,
                $"{Version()}/{Uri.EscapeDataString(_options.WhatsAppBusinessAccountId)}/subscribed_apps",
                accessToken,
                null,
                ct);
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return false;
            return data.EnumerateArray().Any(item =>
            {
                if (FixedEquals(ReadString(item, "id"), _options.AppId)) return true;
                return item.TryGetProperty("whatsapp_business_api_data", out var nested) &&
                       FixedEquals(ReadString(nested, "id"), _options.AppId);
            });
        }

        private async Task<HashSet<string>> GetWhatsAppWebhookFieldsAsync(CancellationToken ct)
        {
            var appAccessToken = $"{_options.AppId}|{_options.AppSecret}";
            var root = await SendGraphAsync(HttpMethod.Get,
                // Meta may ignore the object query parameter and return the default
                // `user` subscription only. Read the complete list and select the
                // WhatsApp subscription below, just as the production diagnostic does.
                $"{Version()}/{Uri.EscapeDataString(_options.AppId)}/subscriptions",
                appAccessToken,
                null,
                ct);
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return result;
            foreach (var subscription in data.EnumerateArray())
            {
                if (!string.Equals(ReadString(subscription, "object"), "whatsapp_business_account", StringComparison.OrdinalIgnoreCase) ||
                    !subscription.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array) continue;
                foreach (var field in fields.EnumerateArray())
                {
                    var name = field.ValueKind == JsonValueKind.String ? field.GetString() : ReadString(field, "name");
                    if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
                }
            }
            return result;
        }

        private async Task<JsonElement> SendGraphAsync(
            HttpMethod method,
            string endpoint,
            string? accessToken,
            HttpContent? content,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, endpoint) { Content = content };
            if (!string.IsNullOrWhiteSpace(accessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new MetaGraphException(response.StatusCode);
            try
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new MetaGraphException(HttpStatusCode.BadGateway);
            }
        }

        private WhatsAppOnboardingStatusDto EmptyStatus(string error) => new(
            IsConfigured(),
            false,
            _options.AppId,
            _options.WhatsAppEmbeddedSignupConfigurationId,
            _options.WhatsAppOAuthRedirectUri,
            _options.WhatsAppBusinessAccountId,
            _options.WhatsAppPhoneNumberId,
            null, null, null, null, null, null, null,
            false,
            false,
            false,
            false,
            false,
            error);

        private async Task TryRequestHistorySyncAsync(int actorAdminUserId, CancellationToken ct)
        {
            try
            {
                var result = await _historySync.RequestAsync(_options.WhatsAppPhoneNumberId, actorAdminUserId, ct);
                if (!result.Success)
                    _logger.LogWarning("WhatsApp onboarding completed, but the one-time history synchronization request was not accepted: {Code}.", result.Error?.Code);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "WhatsApp onboarding completed, but history synchronization could not be started.");
            }
        }

        private bool IsConfigured() =>
            _options.Enabled &&
            !string.IsNullOrWhiteSpace(_options.AppId) &&
            !string.IsNullOrWhiteSpace(_options.AppSecret) &&
            !string.IsNullOrWhiteSpace(_options.WhatsAppAccessToken) &&
            IsNumericId(_options.WhatsAppBusinessAccountId) &&
            IsNumericId(_options.WhatsAppPhoneNumberId) &&
            IsNumericId(_options.WhatsAppEmbeddedSignupConfigurationId) &&
            Uri.TryCreate(_options.WhatsAppOAuthRedirectUri, UriKind.Absolute, out var redirectUri) &&
            string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        private string Version()
        {
            var version = (_options.GraphApiVersion ?? string.Empty).Trim().Trim('/');
            return Regex.IsMatch(version, "^v[0-9]+(?:\\.[0-9]+)?$", RegexOptions.CultureInvariant) ? version : "v26.0";
        }

        private static bool IsNumericId(string? value) => !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit) && value.Length <= 32;
        private static bool FixedEquals(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property)) return null;
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };
        }

        private static ApiResponse<WhatsAppOnboardingResultDto> Error(string details) =>
            ApiResponse<WhatsAppOnboardingResultDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, details);

        private async Task WriteAuditSafelyAsync(int actorAdminUserId, string action, string phoneNumberId, bool succeeded, CancellationToken ct)
        {
            try
            {
                await _audit.WriteAsync(actorAdminUserId, null, action, "WhatsAppPhone", phoneNumberId, null, succeeded, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not persist WhatsApp onboarding audit event {Action}.", action);
            }
        }

        private sealed class MetaGraphException : Exception
        {
            public MetaGraphException(HttpStatusCode statusCode) : base($"Meta Graph API returned HTTP {(int)statusCode}.") => StatusCode = statusCode;
            public HttpStatusCode StatusCode { get; }
        }
    }
}
