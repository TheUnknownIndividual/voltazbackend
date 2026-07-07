using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos.Seo;
using Volt.Application.Interfaces;

namespace Volt.API.Services
{
    public sealed class SeoSubmissionService : ISeoSubmissionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<SeoOptions> _options;
        private readonly ILogger<SeoSubmissionService> _logger;
        private readonly object _searchConsoleLock = new();
        private readonly SemaphoreSlim _googleTokenLock = new(1, 1);
        private DateTimeOffset _lastSearchConsoleSubmit = DateTimeOffset.MinValue;
        private string _cachedGoogleAccessToken = string.Empty;
        private DateTimeOffset _cachedGoogleAccessTokenExpiresAt = DateTimeOffset.MinValue;

        public SeoSubmissionService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<SeoOptions> options,
            ILogger<SeoSubmissionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task SubmitProductCreatedAsync(SeoProductCreatedNotification notification, CancellationToken ct = default)
        {
            var options = _options.CurrentValue;
            var productUrl = BuildProductUrl(options, notification.ProductId);

            await SubmitIndexNowAsync(options, productUrl, ct);
            await SubmitSearchConsoleSitemapAsync(options, ct);
        }

        private async Task SubmitIndexNowAsync(SeoOptions options, string productUrl, CancellationToken ct)
        {
            if (!options.EnableIndexNow || string.IsNullOrWhiteSpace(options.IndexNowKey))
            {
                return;
            }

            try
            {
                var siteBaseUrl = NormalizeSiteBaseUrl(options);
                var siteUri = new Uri(siteBaseUrl);
                var payload = new
                {
                    host = siteUri.Host,
                    key = options.IndexNowKey.Trim(),
                    keyLocation = $"{siteBaseUrl.TrimEnd('/')}/{options.IndexNowKey.Trim()}.txt",
                    urlList = new[] { productUrl }
                };

                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await _httpClientFactory.CreateClient("seo").PostAsync(
                    string.IsNullOrWhiteSpace(options.IndexNowEndpoint)
                        ? "https://api.indexnow.org/indexnow"
                        : options.IndexNowEndpoint,
                    content,
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IndexNow returned {StatusCode} for {ProductUrl}.", response.StatusCode, productUrl);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "IndexNow submission failed for {ProductUrl}.", productUrl);
            }
        }

        private async Task SubmitSearchConsoleSitemapAsync(SeoOptions options, CancellationToken ct)
        {
            if (!options.EnableSearchConsoleSitemapSubmit ||
                (string.IsNullOrWhiteSpace(options.SearchConsoleAccessToken) &&
                 string.IsNullOrWhiteSpace(options.SearchConsoleServiceAccountJsonPath)))
            {
                return;
            }

            if (!CanSubmitSearchConsole(options))
            {
                return;
            }

            try
            {
                var siteUrl = string.IsNullOrWhiteSpace(options.SearchConsoleSiteUrl)
                    ? NormalizeSiteBaseUrl(options)
                    : options.SearchConsoleSiteUrl.Trim();
                var sitemapUrl = string.IsNullOrWhiteSpace(options.SearchConsoleSitemapUrl)
                    ? $"{NormalizeSiteBaseUrl(options).TrimEnd('/')}/sitemap.xml"
                    : options.SearchConsoleSitemapUrl.Trim();

                var requestUrl =
                    $"https://www.googleapis.com/webmasters/v3/sites/{Uri.EscapeDataString(siteUrl)}/sitemaps/{Uri.EscapeDataString(sitemapUrl)}";

                var accessToken = await GetSearchConsoleAccessTokenAsync(options, ct);
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return;
                }

                using var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await _httpClientFactory.CreateClient("seo").SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Search Console sitemap submit returned {StatusCode}.", response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Search Console sitemap submission failed.");
            }
        }

        private async Task<string> GetSearchConsoleAccessTokenAsync(SeoOptions options, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(options.SearchConsoleAccessToken))
            {
                return options.SearchConsoleAccessToken.Trim();
            }

            await _googleTokenLock.WaitAsync(ct);
            try
            {
                if (!string.IsNullOrWhiteSpace(_cachedGoogleAccessToken) &&
                    DateTimeOffset.UtcNow < _cachedGoogleAccessTokenExpiresAt.AddMinutes(-5))
                {
                    return _cachedGoogleAccessToken;
                }

                var credential = await LoadServiceAccountCredentialAsync(options.SearchConsoleServiceAccountJsonPath, ct);
                if (credential is null)
                {
                    return string.Empty;
                }

                var assertion = CreateServiceAccountAssertion(credential);
                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion
                };

                using var response = await _httpClientFactory.CreateClient("seo").PostAsync(
                    string.IsNullOrWhiteSpace(credential.TokenUri)
                        ? "https://oauth2.googleapis.com/token"
                        : credential.TokenUri,
                    new FormUrlEncodedContent(form),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Google OAuth token exchange returned {StatusCode}.", response.StatusCode);
                    return string.Empty;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                var token = await JsonSerializer.DeserializeAsync<GoogleTokenResponse>(stream, cancellationToken: ct);
                if (string.IsNullOrWhiteSpace(token?.AccessToken))
                {
                    _logger.LogWarning("Google OAuth token exchange did not return an access token.");
                    return string.Empty;
                }

                _cachedGoogleAccessToken = token.AccessToken;
                _cachedGoogleAccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn));
                return _cachedGoogleAccessToken;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Unable to create Google Search Console access token from service account.");
                return string.Empty;
            }
            finally
            {
                _googleTokenLock.Release();
            }
        }

        private async Task<GoogleServiceAccountCredential?> LoadServiceAccountCredentialAsync(string path, CancellationToken ct)
        {
            var resolvedPath = ResolveCredentialPath(path);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                _logger.LogWarning("Search Console service account JSON path is not configured or does not exist.");
                return null;
            }

            await using var stream = File.OpenRead(resolvedPath);
            var credential = await JsonSerializer.DeserializeAsync<GoogleServiceAccountCredential>(stream, cancellationToken: ct);
            if (credential is null ||
                string.IsNullOrWhiteSpace(credential.ClientEmail) ||
                string.IsNullOrWhiteSpace(credential.PrivateKey))
            {
                _logger.LogWarning("Search Console service account JSON is missing client_email or private_key.");
                return null;
            }

            return credential;
        }

        private static string ResolveCredentialPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var trimmed = path.Trim();
            if (Path.IsPathRooted(trimmed) || File.Exists(trimmed))
            {
                return trimmed;
            }

            var appBasePath = Path.Combine(AppContext.BaseDirectory, trimmed);
            return File.Exists(appBasePath) ? appBasePath : trimmed;
        }

        private static string CreateServiceAccountAssertion(GoogleServiceAccountCredential credential)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = new Dictionary<string, object>
            {
                ["alg"] = "RS256",
                ["typ"] = "JWT"
            };

            var payload = new Dictionary<string, object>
            {
                ["iss"] = credential.ClientEmail,
                ["scope"] = "https://www.googleapis.com/auth/webmasters",
                ["aud"] = string.IsNullOrWhiteSpace(credential.TokenUri)
                    ? "https://oauth2.googleapis.com/token"
                    : credential.TokenUri,
                ["iat"] = now,
                ["exp"] = now + 3600
            };

            var unsignedJwt = $"{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload))}";

            using var rsa = RSA.Create();
            rsa.ImportFromPem(credential.PrivateKey);
            var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsignedJwt), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{unsignedJwt}.{Base64UrlEncode(signature)}";
        }

        private static string Base64UrlEncode(byte[] value)
            => Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private bool CanSubmitSearchConsole(SeoOptions options)
        {
            var throttle = TimeSpan.FromMinutes(Math.Max(1, options.SearchConsoleSubmitThrottleMinutes));
            lock (_searchConsoleLock)
            {
                if (DateTimeOffset.UtcNow - _lastSearchConsoleSubmit < throttle)
                {
                    return false;
                }

                _lastSearchConsoleSubmit = DateTimeOffset.UtcNow;
                return true;
            }
        }

        private static string BuildProductUrl(SeoOptions options, int productId)
            => $"{NormalizeSiteBaseUrl(options).TrimEnd('/')}/product/{productId}";

        private static string NormalizeSiteBaseUrl(SeoOptions options)
            => string.IsNullOrWhiteSpace(options.SiteBaseUrl)
                ? "https://volt.az"
                : options.SiteBaseUrl.Trim().TrimEnd('/');

        private sealed class GoogleServiceAccountCredential
        {
            [JsonPropertyName("client_email")]
            public string ClientEmail { get; set; } = string.Empty;

            [JsonPropertyName("private_key")]
            public string PrivateKey { get; set; } = string.Empty;

            [JsonPropertyName("token_uri")]
            public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";
        }

        private sealed class GoogleTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
}
