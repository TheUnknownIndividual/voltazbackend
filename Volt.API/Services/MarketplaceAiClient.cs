using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;

namespace Volt.API.Services
{
    // One-shot OpenAI Responses call with a strict JSON schema, shared by the marketplace listing builders.
    public sealed class MarketplaceAiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _client;
        private readonly MarketplaceListingsOptions _options;
        private readonly ProductAiImportOptions _productAiOptions;
        private readonly ILogger<MarketplaceAiClient> _logger;

        public MarketplaceAiClient(
            HttpClient client,
            IOptions<MarketplaceListingsOptions> options,
            IOptions<ProductAiImportOptions> productAiOptions,
            ILogger<MarketplaceAiClient> logger)
        {
            _client = client;
            _options = options.Value;
            _productAiOptions = productAiOptions.Value;
            _logger = logger;
        }

        public async Task<T> GenerateAsync<T>(string schemaName, string prompt, JsonObject schema, CancellationToken ct)
        {
            var apiKey = !string.IsNullOrWhiteSpace(_options.ApiKey) ? _options.ApiKey : _productAiOptions.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("MARKETPLACE_AI_API_KEY_MISSING");
            var model = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : _productAiOptions.Model;

            var payload = new JsonObject
            {
                ["model"] = model,
                ["store"] = false,
                ["max_output_tokens"] = Math.Clamp(_options.MaxOutputTokens, 1000, 20000),
                ["reasoning"] = new JsonObject { ["effort"] = NormalizeReasoningEffort(_options.ReasoningEffort) },
                ["input"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject { ["type"] = "input_text", ["text"] = prompt },
                        },
                    },
                },
                ["text"] = new JsonObject
                {
                    ["verbosity"] = "medium",
                    ["format"] = new JsonObject
                    {
                        ["type"] = "json_schema",
                        ["name"] = schemaName,
                        ["strict"] = true,
                        ["schema"] = schema,
                    },
                },
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, "responses")
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 20, 300)));

            HttpResponseMessage response;
            string json;
            try
            {
                response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
                json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException("MARKETPLACE_AI_REQUEST_TIMEOUT");
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Marketplace AI call rejected with status {Status}", (int)response.StatusCode);
                    throw new InvalidOperationException("MARKETPLACE_AI_API_REJECTED");
                }

                using var document = JsonDocument.Parse(json);
                var contentItems = document.RootElement.GetProperty("output")
                    .EnumerateArray()
                    .Where(x => x.TryGetProperty("content", out _))
                    .SelectMany(x => x.GetProperty("content").EnumerateArray())
                    .ToList();
                if (contentItems.Any(x => x.TryGetProperty("type", out var type) && type.GetString() == "refusal"))
                    throw new InvalidOperationException("MARKETPLACE_AI_OUTPUT_REFUSED");

                var text = contentItems
                    .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "output_text")
                    .Select(x => x.GetProperty("text").GetString())
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("MARKETPLACE_AI_EMPTY_OUTPUT");

                return JsonSerializer.Deserialize<T>(text, JsonOptions)
                    ?? throw new InvalidOperationException("MARKETPLACE_AI_EMPTY_OUTPUT");
            }
        }

        private static string NormalizeReasoningEffort(string? value)
            => value?.Trim().ToLowerInvariant() switch
            {
                "minimal" or "low" or "medium" or "high" => value.Trim().ToLowerInvariant(),
                _ => "low",
            };
    }
}
