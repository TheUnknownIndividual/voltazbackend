using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;

namespace Volt.API.Services
{
    public sealed record SocialPublishResult(bool Ok, string? ExternalId, string? PermalinkUrl, string? Error, bool Retryable);

    // Publishes a single photo post to the Facebook Page (Graph API) and to Instagram
    // (Instagram API with Instagram Login). Tokens are sent in the Authorization header only
    // and never logged; errors are reduced to Graph error code + message.
    public sealed class MetaSocialPublisher
    {
        private const string FacebookHost = "https://graph.facebook.com/";
        private const string InstagramHost = "https://graph.instagram.com/";

        private readonly HttpClient _client;
        private readonly SocialPostingOptions _options;
        private readonly ILogger<MetaSocialPublisher> _logger;

        // A system-user token is exchanged once for the Page token; both are cached for the process lifetime.
        private string? _pageToken;

        public MetaSocialPublisher(
            HttpClient client,
            IOptions<SocialPostingOptions> options,
            ILogger<MetaSocialPublisher> logger)
        {
            _client = client;
            _options = options.Value;
            _logger = logger;
        }

        public bool FacebookConfigured
            => !string.IsNullOrWhiteSpace(_options.FacebookPageId) && !string.IsNullOrWhiteSpace(_options.FacebookPageAccessToken);

        public bool InstagramConfigured
            => !string.IsNullOrWhiteSpace(_options.InstagramUserId) && !string.IsNullOrWhiteSpace(_options.InstagramAccessToken);

        private string Version
            => string.IsNullOrWhiteSpace(_options.GraphApiVersion) ? "v26.0" : _options.GraphApiVersion.Trim().Trim('/');

        public async Task<SocialPublishResult> PublishFacebookAsync(string imageUrl, string caption, CancellationToken ct)
        {
            if (!FacebookConfigured)
                return new SocialPublishResult(false, null, null, "Facebook is not configured.", false);
            try
            {
                var token = await ResolvePageTokenAsync(ct);
                var url = $"{FacebookHost}{Version}/{Uri.EscapeDataString(_options.FacebookPageId)}/photos";
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(new { url = imageUrl, caption, published = true }),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await _client.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode) return Failure("Facebook", response, body);

                using var doc = JsonDocument.Parse(body);
                var postId = ReadString(doc.RootElement, "post_id") ?? ReadString(doc.RootElement, "id");
                if (string.IsNullOrWhiteSpace(postId))
                    return new SocialPublishResult(false, null, null, "Facebook returned no post id.", true);
                return new SocialPublishResult(true, postId, $"https://www.facebook.com/{postId}", null, false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                _logger.LogWarning("Facebook publish failed: {Error}", ex.GetType().Name);
                return new SocialPublishResult(false, null, null, $"Facebook request failed ({ex.GetType().Name}).", true);
            }
        }

        public async Task<SocialPublishResult> PublishInstagramAsync(string imageUrl, string caption, CancellationToken ct)
        {
            if (!InstagramConfigured)
                return new SocialPublishResult(false, null, null, "Instagram is not configured.", false);
            try
            {
                var userId = Uri.EscapeDataString(_options.InstagramUserId);

                // 1) create the media container
                var createResponse = await SendInstagramAsync(
                    HttpMethod.Post, $"{InstagramHost}{Version}/{userId}/media",
                    new { image_url = imageUrl, caption }, ct);
                if (!createResponse.Ok) return createResponse.Result!;
                var containerId = ReadString(createResponse.Json!.RootElement, "id");
                createResponse.Json.Dispose();
                if (string.IsNullOrWhiteSpace(containerId))
                    return new SocialPublishResult(false, null, null, "Instagram returned no container id.", true);

                // 2) wait for the container to finish processing (bounded)
                var ready = false;
                for (var attempt = 0; attempt < 10 && !ready; attempt++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt == 0 ? 2 : 4), ct);
                    var status = await SendInstagramAsync(
                        HttpMethod.Get, $"{InstagramHost}{Version}/{Uri.EscapeDataString(containerId)}?fields=status_code", null, ct);
                    if (!status.Ok) return status.Result!;
                    var code = ReadString(status.Json!.RootElement, "status_code");
                    status.Json.Dispose();
                    if (string.Equals(code, "FINISHED", StringComparison.OrdinalIgnoreCase)) ready = true;
                    else if (string.Equals(code, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(code, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                        return new SocialPublishResult(false, null, null, $"Instagram container status {code}.", false);
                }
                if (!ready)
                    return new SocialPublishResult(false, null, null, "Instagram container was not ready in time.", true);

                // 3) publish it
                var publish = await SendInstagramAsync(
                    HttpMethod.Post, $"{InstagramHost}{Version}/{userId}/media_publish",
                    new { creation_id = containerId }, ct);
                if (!publish.Ok) return publish.Result!;
                var mediaId = ReadString(publish.Json!.RootElement, "id");
                publish.Json.Dispose();
                if (string.IsNullOrWhiteSpace(mediaId))
                    return new SocialPublishResult(false, null, null, "Instagram returned no media id.", true);

                // 4) best-effort permalink
                string? permalink = null;
                var link = await SendInstagramAsync(
                    HttpMethod.Get, $"{InstagramHost}{Version}/{Uri.EscapeDataString(mediaId)}?fields=permalink", null, ct);
                if (link.Ok)
                {
                    permalink = ReadString(link.Json!.RootElement, "permalink");
                    link.Json.Dispose();
                }
                return new SocialPublishResult(true, mediaId, permalink, null, false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                _logger.LogWarning("Instagram publish failed: {Error}", ex.GetType().Name);
                return new SocialPublishResult(false, null, null, $"Instagram request failed ({ex.GetType().Name}).", true);
            }
        }

        private async Task<(bool Ok, JsonDocument? Json, SocialPublishResult? Result)> SendInstagramAsync(
            HttpMethod method, string url, object? payload, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, url);
            if (payload is not null) request.Content = JsonContent.Create(payload);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.InstagramAccessToken);
            using var response = await _client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return (false, null, Failure("Instagram", response, body));
            return (true, JsonDocument.Parse(body), null);
        }

        // The configured Facebook token may be a system-user token; exchange it for the Page token.
        // If the exchange fails the configured token is used as-is (it may already be a Page token).
        private async Task<string> ResolvePageTokenAsync(CancellationToken ct)
        {
            if (_pageToken is not null) return _pageToken;
            try
            {
                var url = $"{FacebookHost}{Version}/{Uri.EscapeDataString(_options.FacebookPageId)}?fields=access_token";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.FacebookPageAccessToken);
                using var response = await _client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    var pageToken = ReadString(doc.RootElement, "access_token");
                    if (!string.IsNullOrWhiteSpace(pageToken))
                    {
                        _pageToken = pageToken;
                        return pageToken;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                _logger.LogInformation("Page token exchange failed ({Error}); using the configured token as-is.", ex.GetType().Name);
            }
            _pageToken = _options.FacebookPageAccessToken;
            return _pageToken;
        }

        private SocialPublishResult Failure(string platform, HttpResponseMessage response, string body)
        {
            var code = 0;
            var message = string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("code", out var c) && c.TryGetInt32(out var parsed)) code = parsed;
                    message = ReadString(error, "message") ?? string.Empty;
                }
            }
            catch (JsonException) { }

            if (message.Length > 200) message = message[..200];
            _logger.LogWarning("{Platform} rejected the request: HTTP {Status}, Graph code {Code}: {Message}",
                platform, (int)response.StatusCode, code, message);
            // Auth/permission problems (190, 10, 200-299) and content rejections will not fix themselves on retry.
            var retryable = (int)response.StatusCode >= 500 || code is 1 or 2 or 4 or 17 or 32 or 341;
            return new SocialPublishResult(false, null, null, $"{platform} error {code}: {message}".Trim(), retryable);
        }

        private static string? ReadString(JsonElement element, string name)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
