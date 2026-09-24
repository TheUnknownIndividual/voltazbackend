using System.Net.Http.Headers;
using Volt.Application.Configuration;

namespace Volt.Infrastructure.Services.NewsScraping
{
    public static class RelayFetch
    {
        public static async Task<string> GetStringAsync(
            HttpClient client, RenewableNewsScraperOptions options, string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(options.RelayBaseUrl))
            {
                return await client.GetStringAsync(url, ct);
            }

            using var response = await SendViaRelayAsync(client, options, url, HttpCompletionOption.ResponseContentRead, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        public static Task<HttpResponseMessage> GetResponseAsync(
            HttpClient client, RenewableNewsScraperOptions options, Uri uri, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(options.RelayBaseUrl))
            {
                return client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            }

            return SendViaRelayAsync(client, options, uri.ToString(), HttpCompletionOption.ResponseHeadersRead, ct);
        }

        private static Task<HttpResponseMessage> SendViaRelayAsync(
            HttpClient client, RenewableNewsScraperOptions options, string url,
            HttpCompletionOption completion, CancellationToken ct)
        {
            var relayUrl = $"{options.RelayBaseUrl!.TrimEnd('/')}/fetch?url={Uri.EscapeDataString(url)}";
            var request = new HttpRequestMessage(HttpMethod.Get, relayUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.RelayAuthToken);
            return client.SendAsync(request, completion, ct);
        }
    }
}
