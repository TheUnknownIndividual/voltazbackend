using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Infrastructure.Data;
using LinkedInCredentialEntity = Volt.Domain.Entities.LinkedInCredential;

namespace Volt.API.Services
{
    public sealed record LinkedInStatusInfo(
        bool AppConfigured, bool Connected, string? OrganizationUrn,
        DateTime? AccessTokenExpiresAtUtc, DateTime? RefreshTokenExpiresAtUtc, bool NeedsReconnect);

    // Posts to the Volt.az LinkedIn Company Page (Community Management API). Unlike MetaSocialPublisher,
    // the live credential is read from the database on every call (never cached), because a refreshed
    // token must take effect immediately without an app-pool restart -- see LinkedInCredential.
    public sealed class LinkedInSocialPublisher
    {
        private const string ApiHost = "https://api.linkedin.com/";
        private const string OAuthAuthorizeUrl = "https://www.linkedin.com/oauth/v2/authorization";
        private const string OAuthTokenUrl = "https://www.linkedin.com/oauth/v2/accessToken";
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromHours(24);

        private readonly HttpClient _client;
        private readonly DataContext _context;
        private readonly SocialPostingOptions _options;
        private readonly ILogger<LinkedInSocialPublisher> _logger;

        public LinkedInSocialPublisher(
            HttpClient client, DataContext context, IOptions<SocialPostingOptions> options,
            ILogger<LinkedInSocialPublisher> logger)
        {
            _client = client;
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        public bool AppConfigured
            => !string.IsNullOrWhiteSpace(_options.LinkedInClientId)
               && !string.IsNullOrWhiteSpace(_options.LinkedInClientSecret)
               && !string.IsNullOrWhiteSpace(_options.LinkedInRedirectUri);

        // Cheap check used by the runner each tick to decide whether to include LinkedIn at all.
        public async Task<bool> IsConnectedAsync(CancellationToken ct)
            => AppConfigured && await _context.LinkedInCredential.AsNoTracking()
                .AnyAsync(x => x.Id == 1 && x.AccessToken != null && x.OrganizationUrn != null, ct);

        public string BuildAuthorizeUrl(string state)
        {
            var scope = Uri.EscapeDataString("w_organization_social rw_organization_admin");
            return $"{OAuthAuthorizeUrl}?response_type=code" +
                   $"&client_id={Uri.EscapeDataString(_options.LinkedInClientId)}" +
                   $"&redirect_uri={Uri.EscapeDataString(_options.LinkedInRedirectUri)}" +
                   $"&state={Uri.EscapeDataString(state)}&scope={scope}";
        }

        public async Task<LinkedInStatusInfo> GetStatusAsync(CancellationToken ct)
        {
            var row = await _context.LinkedInCredential.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
            var connected = row?.AccessToken is not null && row.OrganizationUrn is not null;
            var needsReconnect = connected && row!.RefreshTokenExpiresAtUtc is { } refreshExpiry && refreshExpiry < DateTime.UtcNow;
            return new LinkedInStatusInfo(
                AppConfigured, connected, row?.OrganizationUrn,
                row?.AccessTokenExpiresAtUtc, row?.RefreshTokenExpiresAtUtc, needsReconnect);
        }

        // Exchanges the one-time authorization code, looks up the admin's organization, and saves
        // (or replaces) the single credential row. Called once by the admin's "Connect" flow, and
        // again whenever the refresh token has expired and the admin reconnects.
        public async Task<(bool Ok, string? Error)> ConnectAsync(string code, int adminId, CancellationToken ct)
        {
            if (!AppConfigured) return (false, "LinkedIn app is not configured on the server.");
            try
            {
                var tokenResponse = await PostFormAsync(OAuthTokenUrl, new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["client_id"] = _options.LinkedInClientId,
                    ["client_secret"] = _options.LinkedInClientSecret,
                    ["redirect_uri"] = _options.LinkedInRedirectUri,
                }, ct);
                if (tokenResponse is null) return (false, "LinkedIn did not return a token.");
                var (accessToken, refreshToken, accessExpiresAt, refreshExpiresAt) = tokenResponse.Value;

                var organizationUrn = await FetchOrganizationUrnAsync(accessToken, ct);
                if (organizationUrn is null) return (false, "No LinkedIn organization with admin access was found for this account.");

                var row = await _context.LinkedInCredential.FirstOrDefaultAsync(x => x.Id == 1, ct);
                if (row is null)
                {
                    row = new LinkedInCredentialEntity { Id = 1 };
                    _context.LinkedInCredential.Add(row);
                }
                row.OrganizationUrn = organizationUrn;
                row.AccessToken = accessToken;
                row.RefreshToken = refreshToken;
                row.AccessTokenExpiresAtUtc = accessExpiresAt;
                row.RefreshTokenExpiresAtUtc = refreshExpiresAt;
                row.ConnectedByAdminId = adminId;
                row.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return (true, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                _logger.LogWarning("LinkedIn connect failed: {Error}", ex.GetType().Name);
                return (false, "Could not complete the LinkedIn connection.");
            }
        }

        // Ensures the background refresh job (and the publish path below) keep the access token current.
        public async Task RefreshIfNeededAsync(CancellationToken ct)
        {
            var row = await _context.LinkedInCredential.FirstOrDefaultAsync(x => x.Id == 1, ct);
            if (row?.AccessToken is null || row.RefreshToken is null) return;
            await EnsureFreshTokenAsync(row, ct);
        }

        public async Task<SocialPublishResult> PublishAsync(string imageUrl, string caption, CancellationToken ct)
        {
            if (!AppConfigured) return new SocialPublishResult(false, null, null, "LinkedIn app is not configured.", false);
            var row = await _context.LinkedInCredential.FirstOrDefaultAsync(x => x.Id == 1, ct);
            if (row?.AccessToken is null || row.OrganizationUrn is null)
                return new SocialPublishResult(false, null, null, "LinkedIn is not connected.", false);

            if (!await EnsureFreshTokenAsync(row, ct))
                return new SocialPublishResult(false, null, null, "LinkedIn token refresh failed; reconnect required.", false);

            try
            {
                byte[] imageBytes;
                using (var imageResponse = await _client.GetAsync(imageUrl, ct))
                {
                    if (!imageResponse.IsSuccessStatusCode)
                        return new SocialPublishResult(false, null, null, "Could not download the post image.", true);
                    imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(ct);
                }

                var initBody = JsonContent.Create(new
                {
                    initializeUploadRequest = new { owner = row.OrganizationUrn },
                });
                using var initRequest = NewRequest(HttpMethod.Post, $"{ApiHost}rest/images?action=initializeUpload", row.AccessToken);
                initRequest.Content = initBody;
                using var initResponse = await _client.SendAsync(initRequest, ct);
                var initText = await initResponse.Content.ReadAsStringAsync(ct);
                if (!initResponse.IsSuccessStatusCode) return Failure("LinkedIn image init", initResponse, initText);

                using var initDoc = JsonDocument.Parse(initText);
                var value = initDoc.RootElement.GetProperty("value");
                var uploadUrl = value.GetProperty("uploadUrl").GetString();
                var imageUrn = value.GetProperty("image").GetString();
                if (string.IsNullOrWhiteSpace(uploadUrl) || string.IsNullOrWhiteSpace(imageUrn))
                    return new SocialPublishResult(false, null, null, "LinkedIn did not return an upload target.", true);

                using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
                {
                    Content = new ByteArrayContent(imageBytes),
                };
                uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", row.AccessToken);
                using var uploadResponse = await _client.SendAsync(uploadRequest, ct);
                if (!uploadResponse.IsSuccessStatusCode)
                    return new SocialPublishResult(false, null, null, $"LinkedIn image upload failed with HTTP {(int)uploadResponse.StatusCode}.", true);

                var postBody = JsonContent.Create(new
                {
                    author = row.OrganizationUrn,
                    commentary = caption,
                    visibility = "PUBLIC",
                    distribution = new { feedDistribution = "MAIN_FEED", targetEntities = Array.Empty<object>(), thirdPartyDistributionChannels = Array.Empty<object>() },
                    content = new { media = new { id = imageUrn } },
                    lifecycleState = "PUBLISHED",
                    isReshareDisabledByAuthor = false,
                });
                using var postRequest = NewRequest(HttpMethod.Post, $"{ApiHost}rest/posts", row.AccessToken);
                postRequest.Content = postBody;
                using var postResponse = await _client.SendAsync(postRequest, ct);
                if (!postResponse.IsSuccessStatusCode)
                {
                    var postErrorText = await postResponse.Content.ReadAsStringAsync(ct);
                    return Failure("LinkedIn post", postResponse, postErrorText);
                }

                // LinkedIn returns the created post's id in a response header, not the body.
                var postId = postResponse.Headers.TryGetValues("x-restli-id", out var ids) ? ids.FirstOrDefault() : null;
                if (string.IsNullOrWhiteSpace(postId))
                    return new SocialPublishResult(true, null, null, null, false);

                var permalink = $"https://www.linkedin.com/feed/update/{Uri.EscapeDataString(postId)}/";
                return new SocialPublishResult(true, postId, permalink, null, false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                _logger.LogWarning("LinkedIn publish failed: {Error}", ex.GetType().Name);
                return new SocialPublishResult(false, null, null, $"LinkedIn request failed ({ex.GetType().Name}).", true);
            }
        }

        private async Task<bool> EnsureFreshTokenAsync(LinkedInCredentialEntity row, CancellationToken ct)
        {
            if (row.AccessTokenExpiresAtUtc is { } expiresAt && expiresAt - DateTime.UtcNow > RefreshMargin) return true;
            if (string.IsNullOrWhiteSpace(row.RefreshToken)) return row.AccessTokenExpiresAtUtc is null; // no way to refresh; use what we have
            if (row.RefreshTokenExpiresAtUtc is { } refreshExpiresAt && refreshExpiresAt < DateTime.UtcNow) return false;

            try
            {
                var tokenResponse = await PostFormAsync(OAuthTokenUrl, new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = row.RefreshToken,
                    ["client_id"] = _options.LinkedInClientId,
                    ["client_secret"] = _options.LinkedInClientSecret,
                }, ct);
                if (tokenResponse is null) return false;
                var (accessToken, refreshToken, accessExpiresAt, refreshExpiresAt2) = tokenResponse.Value;

                row.AccessToken = accessToken;
                row.RefreshToken = refreshToken ?? row.RefreshToken;
                row.AccessTokenExpiresAtUtc = accessExpiresAt;
                row.RefreshTokenExpiresAtUtc = refreshExpiresAt2 ?? row.RefreshTokenExpiresAtUtc;
                row.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                _logger.LogWarning("LinkedIn token refresh failed: {Error}", ex.GetType().Name);
                return false;
            }
        }

        private async Task<string?> FetchOrganizationUrnAsync(string accessToken, CancellationToken ct)
        {
            using var request = NewRequest(HttpMethod.Get,
                $"{ApiHost}rest/organizationAcls?q=roleAssignee&role=ADMINISTRATOR&state=APPROVED", accessToken);
            using var response = await _client.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array) return null;
            foreach (var element in elements.EnumerateArray())
                if (element.TryGetProperty("organization", out var org) && org.ValueKind == JsonValueKind.String)
                    return org.GetString();
            return null;
        }

        private async Task<(string AccessToken, string? RefreshToken, DateTime AccessExpiresAt, DateTime? RefreshExpiresAt)?> PostFormAsync(
            string url, Dictionary<string, string> form, CancellationToken ct)
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _client.PostAsync(url, content, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LinkedIn OAuth token request failed: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            if (string.IsNullOrWhiteSpace(accessToken)) return null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt64(out var eiVal) ? eiVal : 3600L;
            var refreshExpiresIn = root.TryGetProperty("refresh_token_expires_in", out var rei) && rei.TryGetInt64(out var reiVal) ? (long?)reiVal : null;
            var accessExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            var refreshExpiresAt = refreshExpiresIn is { } seconds ? DateTime.UtcNow.AddSeconds(seconds) : (DateTime?)null;
            return (accessToken, refreshToken, accessExpiresAt, refreshExpiresAt);
        }

        private static HttpRequestMessage NewRequest(HttpMethod method, string url, string accessToken)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("LinkedIn-Version", "202501");
            request.Headers.TryAddWithoutValidation("X-Restli-Protocol-Version", "2.0.0");
            return request;
        }

        private SocialPublishResult Failure(string label, HttpResponseMessage response, string body)
        {
            var message = body;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    message = m.GetString() ?? body;
            }
            catch (JsonException) { }

            if (message.Length > 200) message = message[..200];
            _logger.LogWarning("{Label} rejected the request: HTTP {Status}: {Message}", label, (int)response.StatusCode, message);
            var retryable = (int)response.StatusCode >= 500;
            return new SocialPublishResult(false, null, null, $"{label} error {(int)response.StatusCode}: {message}".Trim(), retryable);
        }
    }
}
