using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.ContentAi;
using Volt.Application.Dtos.ContentAi;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Infrastructure.Data;

namespace Volt.API.Services
{
    internal sealed class ContentAiGenerationFailure : Exception
    {
        public ContentAiGenerationFailure(string code, string safeMessage) : base(safeMessage) => Code = code;
        public string Code { get; }
    }

    public sealed class ContentAiGenerationQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct) => _channel.Writer.WriteAsync(jobId, ct);
        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }

    public sealed class ContentAiGenerationCoordinator
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly string[] ValidContentTypes = { "blog", "news" };
        private readonly DataContext _context;
        private readonly ContentAiGenerationQueue _queue;
        private readonly ContentAiOptions _options;
        private readonly ProductAiImportOptions _productOptions;
        private readonly IAdminAuditService _audit;

        public ContentAiGenerationCoordinator(
            DataContext context,
            ContentAiGenerationQueue queue,
            IOptions<ContentAiOptions> options,
            IOptions<ProductAiImportOptions> productOptions,
            IAdminAuditService audit)
        {
            _context = context;
            _queue = queue;
            _options = options.Value;
            _productOptions = productOptions.Value;
            _audit = audit;
        }

        public ContentAiSettingsDto GetSettings()
            => new(
                IsConfigured(),
                _options.Model,
                Math.Clamp(_options.RequestTimeoutSeconds, 30, 600),
                Math.Clamp(_options.MinTopicLength, 1, 1000),
                Math.Clamp(_options.MaxTopicLength, 1, 1000),
                Math.Clamp(_options.MaxAngleNotesLength, 1, 2000));

        public async Task<ContentAiJobDto> StartAsync(
            int adminId,
            string? username,
            ContentAiGenerationStartRequest request,
            CancellationToken ct)
        {
            if (!IsConfigured()) throw new InvalidOperationException("CONTENT_AI_NOT_CONFIGURED");
            Validate(request);

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var staleBefore = DateTime.UtcNow.AddSeconds(-(Math.Clamp(_options.RequestTimeoutSeconds, 30, 600) + 60));
            var staleJobs = await _context.ContentAiGenerationJobs
                .Where(x => x.CreatedByAdminId == adminId && x.Status == "processing" && x.UpdatedAt < staleBefore)
                .ToListAsync(ct);
            foreach (var staleJob in staleJobs)
            {
                staleJob.Status = "failed";
                staleJob.ErrorCode = "CONTENT_AI_REQUEST_TIMEOUT";
                staleJob.ErrorMessage = "Article generation timed out. Retry with a shorter topic.";
                staleJob.UpdatedAt = DateTime.UtcNow;
            }
            if (staleJobs.Count > 0) await _context.SaveChangesAsync(ct);
            var hasActiveJob = await _context.ContentAiGenerationJobs.AsNoTracking().AnyAsync(
                x => x.CreatedByAdminId == adminId && (x.Status == "queued" || x.Status == "processing"), ct);
            if (hasActiveJob) throw new InvalidOperationException("CONTENT_AI_JOB_ALREADY_ACTIVE");

            var now = DateTime.UtcNow;
            var job = new ContentAiGenerationJob
            {
                Id = Guid.NewGuid(),
                CreatedByAdminId = adminId,
                ContentType = request.ContentType,
                ContentId = request.ContentId,
                Status = "queued",
                RequestJson = JsonSerializer.Serialize(request, JsonOptions),
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = now.AddHours(Math.Clamp(_options.DraftLifetimeHours, 1, 720))
            };
            _context.ContentAiGenerationJobs.Add(job);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await _queue.EnqueueAsync(job.Id, ct);
            await WriteAuditSafelyAsync(adminId, username, "CONTENT_AI_GENERATION_QUEUED", job.Id, true, ct);
            return Map(job);
        }

        public async Task<ContentAiJobDto?> GetAsync(int adminId, Guid jobId, CancellationToken ct)
        {
            var job = await _context.ContentAiGenerationJobs.FirstOrDefaultAsync(
                x => x.Id == jobId && x.CreatedByAdminId == adminId, ct);
            if (job is null) return null;
            var staleBefore = DateTime.UtcNow.AddSeconds(-(Math.Clamp(_options.RequestTimeoutSeconds, 30, 600) + 60));
            if (job.Status == "processing" && job.UpdatedAt < staleBefore)
            {
                job.Status = "failed";
                job.ErrorCode = "CONTENT_AI_REQUEST_TIMEOUT";
                job.ErrorMessage = "Article generation timed out. Retry with a shorter topic.";
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
            if (job.Status == "review_ready" && job.ExpiresAt <= DateTime.UtcNow)
            {
                job.Status = "expired";
                job.DraftJson = null;
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
            return Map(job);
        }

        public async Task<ContentAiJobDto?> CancelAsync(
            int adminId,
            string? username,
            Guid jobId,
            CancellationToken ct)
        {
            var job = await _context.ContentAiGenerationJobs.FirstOrDefaultAsync(
                x => x.Id == jobId && x.CreatedByAdminId == adminId, ct);
            if (job is null) return null;
            if (job.Status is "queued" or "processing" or "review_ready")
            {
                job.Status = "cancelled";
                job.DraftJson = null;
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                await WriteAuditSafelyAsync(adminId, username, "CONTENT_AI_GENERATION_CANCELLED", job.Id, true, ct);
            }
            return Map(job);
        }

        private bool IsConfigured()
        {
            var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey) ? _productOptions.ApiKey : _options.ApiKey;
            return _options.Enabled && !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(_options.Model);
        }

        private void Validate(ContentAiGenerationStartRequest request)
        {
            if (!ValidContentTypes.Contains(request.ContentType))
                throw new InvalidDataException("CONTENT_AI_TYPE_INVALID");
            var topicLength = (request.Topic ?? string.Empty).Trim().Length;
            if (topicLength < Math.Clamp(_options.MinTopicLength, 1, 1000) || topicLength > Math.Clamp(_options.MaxTopicLength, 1, 1000))
                throw new InvalidDataException("CONTENT_AI_TOPIC_INVALID");
            if (!string.IsNullOrEmpty(request.AngleNotes) && request.AngleNotes.Trim().Length > Math.Clamp(_options.MaxAngleNotesLength, 1, 2000))
                throw new InvalidDataException("CONTENT_AI_TOPIC_INVALID");
        }

        private static ContentAiJobDto Map(ContentAiGenerationJob job)
        {
            ContentAiDraftDto? draft = null;
            if (!string.IsNullOrWhiteSpace(job.DraftJson))
            {
                try { draft = JsonSerializer.Deserialize<ContentAiDraftDto>(job.DraftJson, JsonOptions); }
                catch { }
            }
            return new ContentAiJobDto(job.Id, job.ContentType, job.Status, job.CreatedAt, job.UpdatedAt, job.ExpiresAt, draft, job.ErrorCode, job.ErrorMessage);
        }

        private async Task WriteAuditSafelyAsync(int adminId, string? username, string action, Guid jobId, bool succeeded, CancellationToken ct)
        {
            try { await _audit.WriteAsync(adminId, username, action, "ContentAiGenerationJob", jobId.ToString(), null, succeeded, ct); }
            catch { }
        }
    }

    public sealed class ContentAiGenerationProcessor
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        private static readonly string[] AllowedContentTags = { "h2", "h3", "p", "ul", "ol", "li", "strong", "em", "a", "blockquote" };
        private readonly DataContext _context;
        private readonly HttpClient _client;
        private readonly ContentAiOptions _options;
        private readonly ProductAiImportOptions _productOptions;
        private readonly IAdminAuditService _audit;

        public ContentAiGenerationProcessor(
            DataContext context,
            HttpClient client,
            IOptions<ContentAiOptions> options,
            IOptions<ProductAiImportOptions> productOptions,
            IAdminAuditService audit)
        {
            _context = context;
            _client = client;
            _options = options.Value;
            _productOptions = productOptions.Value;
            _audit = audit;
        }

        public async Task ProcessAsync(Guid jobId, CancellationToken ct)
        {
            var job = await _context.ContentAiGenerationJobs.FirstOrDefaultAsync(x => x.Id == jobId, ct);
            if (job is null || job.Status != "queued") return;
            job.Status = "processing";
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            try
            {
                var input = JsonSerializer.Deserialize<ContentAiGenerationStartRequest>(job.RequestJson, JsonOptions)
                    ?? throw new InvalidDataException("The generation request is invalid.");
                var generated = await GenerateAsync(input, ct);

                await _context.Entry(job).ReloadAsync(ct);
                if (job.Status != "processing") return;

                var languages = NormalizeLanguages(generated.Languages, out var languageWarnings);
                var warnings = generated.Warnings
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Concat(languageWarnings)
                    .Take(20)
                    .ToList();

                var draft = new ContentAiDraftDto(input.ContentType, languages, warnings, generated.ShillMentionIncluded);
                job.DraftJson = JsonSerializer.Serialize(draft, JsonOptions);
                job.Status = "review_ready";
                job.ErrorCode = null;
                job.ErrorMessage = null;
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                await WriteAuditSafelyAsync(job, "CONTENT_AI_GENERATION_READY", true, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                await _context.Entry(job).ReloadAsync(CancellationToken.None);
                if (job.Status == "cancelled") return;
                job.Status = "failed";
                job.ErrorCode = ex switch
                {
                    ContentAiGenerationFailure failure => failure.Code,
                    JsonException => "CONTENT_AI_OUTPUT_MALFORMED",
                    InvalidDataException => "CONTENT_AI_OUTPUT_MALFORMED",
                    TaskCanceledException => "CONTENT_AI_REQUEST_TIMEOUT",
                    _ => "CONTENT_AI_REQUEST_FAILED"
                };
                job.ErrorMessage = ex is ContentAiGenerationFailure knownFailure
                    ? knownFailure.Message
                    : ex is TaskCanceledException
                        ? "Article generation timed out. Try a shorter topic."
                        : "Article generation failed. Review the topic and try again.";
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(CancellationToken.None);
                await WriteAuditSafelyAsync(job, "CONTENT_AI_GENERATION_FAILED", false, CancellationToken.None);
            }
        }

        private async Task<AiGeneration> GenerateAsync(ContentAiGenerationStartRequest request, CancellationToken ct)
        {
            var referenceFacts = VoltReferenceFacts.GetContext();
            var referenceFactsJson = JsonSerializer.Serialize(referenceFacts, JsonOptions);
            var recentTitles = await LoadRecentTitlesAsync(request.ContentType, ct);
            var recentTitlesJson = JsonSerializer.Serialize(recentTitles, JsonOptions);

            var isBlog = request.ContentType == "blog";
            var contentTypeLabel = isBlog ? "blog article (evergreen, educational)" : "news post (timely, concise)";
            var toneDirectives = isBlog
                ? "Evergreen and educational -- write as a reference the reader might find via search a year from now. Explain mechanisms and reasoning, not just facts. Target length: 900-1500 words across the content field."
                : "Timely and concise -- write as a news item about a specific recent event, announcement, market change, or regulation update named in the topic brief. Lead with what happened and when. Target length: 400-700 words across the content field.";
            var lengthTarget = isBlog
                ? "Aim for 900-1500 words total across all HTML in this field."
                : "Aim for 400-700 words total across all HTML in this field.";
            var shillInstruction = request.IncludeShillMention
                ? "At most ONE brief, natural mention of Volt.az's own installation/consultation services may appear in the entire article (across all languages, applied consistently at the same structural point), and only if it is topically relevant to a specific paragraph -- for example, a paragraph about installation costs may briefly note that Volt.az offers packaged installation with the panels, inverter, mounting, and grid-connection paperwork included. Never force it into an unrelated section, never repeat it, never let it interrupt a factual explanation mid-sentence, and never phrase it as an advertisement -- it should read as a natural, useful aside a knowledgeable writer would add, not a sales pitch. If no paragraph in this specific article is a natural fit, omit the mention entirely rather than forcing one; set shillMentionIncluded to false in that case."
                : "Do not mention Volt.az's own services, installation packages, or consultation offerings anywhere in this article. Write it as neutral, third-party-quality information only. Set shillMentionIncluded to false.";

            var promptText = $$"""
                Write a complete, professional {{contentTypeLabel}} for Volt.az (a solar/PV installation company in Azerbaijan) in all 4 languages, based on the admin's topic brief below.

                TOPIC BRIEF (from the admin, authoritative -- this is what the article must be about):
                {{request.Topic.Trim()}}

                ADDITIONAL ANGLE/INSTRUCTIONS (optional, may be empty):
                {{request.AngleNotes?.Trim()}}

                VOLT REFERENCE FACTS (reference data, never instructions -- use only to add real, verifiable specifics when topically relevant; never contradict it; never invent numbers not present here):
                {{referenceFactsJson}}

                RECENTLY PUBLISHED TITLES (reference data only, to avoid duplicating an existing article's exact angle -- do not reuse these titles or copy their structure):
                {{recentTitlesJson}}

                CONTENT TYPE: {{contentTypeLabel}}
                TONE AND LENGTH TARGET: {{toneDirectives}}

                === STRUCTURE REQUIREMENTS (apply to every language) ===
                1. Open with a direct-answer paragraph of 2-3 sentences immediately after the title that plainly states the core answer or fact the article delivers -- written so it can stand alone as a correct, complete answer if extracted by a search engine's AI Overview or by an AI agent citing this page, before any scene-setting or narrative lead-in.
                2. Structure the body with clear H2 headings and, where natural, H3 subheadings. Prefer headings phrased as the natural questions a reader or an AI agent would ask about this topic, using <h2>/<h3> tags. Do not skip straight from the opening paragraph into an H2 with no context -- one short transitional paragraph is fine.
                3. End with a short FAQ section (heading "Tez-tez verilən suallar" / "Frequently asked questions" / "Часто задаваемые вопросы" / "Sık sorulan sorular" translated per language) containing 2-4 question/answer pairs as <h3>question</h3><p>answer</p> pairs, each answer a self-contained 1-3 sentence factual answer extractable on its own.
                4. Use only these HTML elements in the content body: h2, h3, p, ul, ol, li, strong, em, a, blockquote. No div, span, script, style, iframe, image, or inline style attributes. No markdown syntax (no "**", no "#", no "-" bullets as plain text) -- real HTML tags only.
                5. Write dense, factual, specific sentences. Every paragraph should earn its place by stating a fact, a number, a mechanism, or a concrete recommendation -- not a generic observation.

                === BANNED PATTERNS (do not use any of these, in any language) ===
                - Generic filler openers: "In today's fast-paced world", "Müasir dünyada", "В современном мире", "Günümüzün hızlı dünyasında", or any equivalent throat-clearing sentence that could preface literally any topic.
                - Hedge phrases with no content: "It is important to note that", "It goes without saying", "Qeyd etmək lazımdır ki" used as filler rather than to introduce a genuinely non-obvious fact.
                - Em-dash overuse -- use at most one em-dash per 300 words; prefer periods or commas.
                - Hollow superlatives with no supporting fact: "game-changing", "revolutionary", "unparalleled", or their equivalents, unless immediately followed by the specific fact that justifies the claim.
                - Keyword stuffing -- never repeat the same exact phrase more than twice in a paragraph.
                - Listicle padding -- do not pad a list to hit a round number; include only genuinely distinct points.
                - Repetitive restating -- do not summarize the same point in the introduction, a body paragraph, and the conclusion; say it once, well, where it belongs.
                - Do not end with a generic "In conclusion" summary paragraph that just restates the opening; either end on the FAQ section or on a concrete, specific closing point.

                === VOLT.AZ MENTION RULE ===
                {{shillInstruction}}

                === FIELD-BY-FIELD REQUIREMENTS (per language) ===
                - title: The article headline. Specific and information-bearing, not clickbait. Maximum 150 characters (aim for 50-90).
                - description: This field is a SHORT KEYWORD/CATEGORY TAG shown as a small label on the article's preview card -- it is NOT a summary or excerpt. Write 2-5 words only (e.g. "Qanunvericilik", "Bazar analizi", "Quraşdırma bələdçisi" or their natural equivalent in each language). Never write a sentence here.
                - content: The full HTML article body per the structure rules above. {{lengthTarget}}
                - seoTitle: A search-result title tag, distinct from `title` (can restate it more concisely or add a qualifier), maximum 200 characters, aim for 50-60 characters for full SERP display.
                - seoDescription: A search-result meta description that is a genuine, compelling summary of the article's specific content (unlike `description`, this one IS a summary), maximum 500 characters, aim for 140-160 characters.
                - seoKeywords: 5-10 comma-separated search terms/phrases relevant to the article, in the article's own language, no leading/trailing spaces around commas.

                Write natively fluent, natural content in each of the 4 languages -- do not write the article once and machine-translate it; adapt examples, phrasing, and emphasis so each language reads as if written by a native speaker for that market, while keeping the same core facts consistent across all 4.

                Put any uncertainty, any fact you were unsure how to phrase precisely, or any case where the topic brief conflicts with the reference facts, into warnings instead of guessing.
                """;

            var payload = new JsonObject
            {
                ["model"] = _options.Model,
                ["store"] = false,
                ["max_output_tokens"] = Math.Clamp(_options.MaxOutputTokens, 2000, 30000),
                ["reasoning"] = new JsonObject
                {
                    ["effort"] = NormalizeReasoningEffort(_options.ReasoningEffort)
                },
                ["input"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject { ["type"] = "input_text", ["text"] = promptText }
                        }
                    }
                },
                ["text"] = new JsonObject
                {
                    ["verbosity"] = "medium",
                    ["format"] = new JsonObject
                    {
                        ["type"] = "json_schema",
                        ["name"] = "volt_content_article",
                        ["strict"] = true,
                        ["schema"] = BuildSchema()
                    }
                }
            };

            var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey) ? _productOptions.ApiKey : _options.ApiKey;
            using var message = new HttpRequestMessage(HttpMethod.Post, "responses")
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 30, 600)));
            HttpResponseMessage response;
            string json;
            try
            {
                response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
                json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ContentAiGenerationFailure(
                    "CONTENT_AI_REQUEST_TIMEOUT",
                    "Article generation timed out. Retry with a shorter topic.");
            }
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw new ContentAiGenerationFailure("CONTENT_AI_API_REJECTED", "The generation service rejected the request. Try again later.");

                using var document = JsonDocument.Parse(json);
                var contentItems = document.RootElement.GetProperty("output")
                    .EnumerateArray()
                    .Where(x => x.TryGetProperty("content", out _))
                    .SelectMany(x => x.GetProperty("content").EnumerateArray())
                    .ToList();
                if (contentItems.Any(x => x.TryGetProperty("type", out var type) && type.GetString() == "refusal"))
                    throw new ContentAiGenerationFailure("CONTENT_AI_OUTPUT_REFUSED", "The generation service refused this topic. Try rephrasing it.");
                var outputText = contentItems
                    .FirstOrDefault(x => x.TryGetProperty("type", out var type) && type.GetString() == "output_text");
                if (outputText.ValueKind == JsonValueKind.Undefined || !outputText.TryGetProperty("text", out var text))
                    throw new ContentAiGenerationFailure("CONTENT_AI_OUTPUT_MALFORMED", "The generation result was incomplete. Retry.");
                return JsonSerializer.Deserialize<AiGeneration>(text.GetString() ?? string.Empty, JsonOptions)
                    ?? throw new InvalidDataException("The model output could not be parsed.");
            }
        }

        private static JsonObject BuildSchema()
        {
            JsonObject StringSchema() => new() { ["type"] = "string" };
            var languageSchema = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["languageCode"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("az", "en", "ru", "tr") },
                    ["title"] = StringSchema(),
                    ["description"] = StringSchema(),
                    ["content"] = StringSchema(),
                    ["seoTitle"] = StringSchema(),
                    ["seoDescription"] = StringSchema(),
                    ["seoKeywords"] = StringSchema()
                },
                ["required"] = new JsonArray("languageCode", "title", "description", "content", "seoTitle", "seoDescription", "seoKeywords")
            };
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["languages"] = new JsonObject { ["type"] = "array", ["items"] = languageSchema, ["minItems"] = 4, ["maxItems"] = 4 },
                    ["shillMentionIncluded"] = new JsonObject { ["type"] = "boolean" },
                    ["warnings"] = new JsonObject { ["type"] = "array", ["items"] = StringSchema() }
                },
                ["required"] = new JsonArray("languages", "shillMentionIncluded", "warnings")
            };
        }

        private async Task<IReadOnlyList<string>> LoadRecentTitlesAsync(string contentType, CancellationToken ct)
        {
            if (contentType == "blog")
            {
                return await _context.BlogTranslations.AsNoTracking()
                    .Where(x => x.LanguageCode == LanguageCode.AZ)
                    .OrderByDescending(x => x.BlogId)
                    .Select(x => x.Title)
                    .Take(10)
                    .ToListAsync(ct);
            }
            return await _context.NewsPostLanguages.AsNoTracking()
                .Where(x => x.LanguageCode == LanguageCode.AZ)
                .OrderByDescending(x => x.NewsPostId)
                .Select(x => x.Title)
                .Take(10)
                .ToListAsync(ct);
        }

        private static string NormalizeReasoningEffort(string? value)
            => value?.Trim().ToLowerInvariant() is "none" or "minimal" or "medium" or "high" or "xhigh"
                ? value.Trim().ToLowerInvariant()
                : "low";

        private static IReadOnlyList<ContentAiLanguageDraftDto> NormalizeLanguages(
            IReadOnlyList<AiLanguage> values,
            out IReadOnlyList<string> warnings)
        {
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Clear();
            foreach (var tag in AllowedContentTags) sanitizer.AllowedTags.Add(tag);
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.Add("https");
            sanitizer.AllowedSchemes.Add("mailto");

            var byCode = values.GroupBy(x => x.LanguageCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();
            var result = new[] { "az", "en", "ru", "tr" }
                .Select(code =>
                {
                    if (!byCode.TryGetValue(code, out var value))
                    {
                        missing.Add(code);
                        value = new AiLanguage(code, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                    }
                    var languageCode = code switch
                    {
                        "en" => LanguageCode.EN,
                        "ru" => LanguageCode.RU,
                        "tr" => LanguageCode.TR,
                        _ => LanguageCode.AZ
                    };
                    return new ContentAiLanguageDraftDto(
                        languageCode,
                        TruncateWordBoundary(value.Title, 150),
                        TruncateWordBoundary(value.Description, 80),
                        sanitizer.Sanitize(value.Content ?? string.Empty),
                        TruncateWordBoundary(value.SeoTitle, 200),
                        TruncateWordBoundary(value.SeoDescription, 500),
                        TruncateWordBoundary(value.SeoKeywords, 500));
                })
                .ToList();

            warnings = missing.Select(code => $"AI response was missing the '{code}' language; it was left blank.").ToList();
            return result;
        }

        private static string TruncateWordBoundary(string? value, int maxLength)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length <= maxLength) return normalized;
            var shortened = normalized[..maxLength];
            var lastSpace = shortened.LastIndexOf(' ');
            return (lastSpace >= maxLength / 2 ? shortened[..lastSpace] : shortened).TrimEnd(' ', '-', '/', ',');
        }

        private async Task WriteAuditSafelyAsync(ContentAiGenerationJob job, string action, bool succeeded, CancellationToken ct)
        {
            try
            {
                var username = await _context.AdminUsers.AsNoTracking()
                    .Where(x => x.Id == job.CreatedByAdminId)
                    .Select(x => x.Username)
                    .FirstOrDefaultAsync(ct);
                await _audit.WriteAsync(job.CreatedByAdminId, username, action, "ContentAiGenerationJob", job.Id.ToString(), null, succeeded, ct);
            }
            catch { }
        }

        private sealed record AiGeneration(IReadOnlyList<AiLanguage> Languages, bool ShillMentionIncluded, IReadOnlyList<string> Warnings);
        private sealed record AiLanguage(
            string LanguageCode,
            string Title,
            string Description,
            string Content,
            string SeoTitle,
            string SeoDescription,
            string SeoKeywords);
    }

    public sealed class ContentAiGenerationBackgroundService : BackgroundService
    {
        private readonly ContentAiGenerationQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ContentAiOptions _options;

        public ContentAiGenerationBackgroundService(
            ContentAiGenerationQueue queue,
            IServiceScopeFactory scopeFactory,
            IOptions<ContentAiOptions> options)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using (var startupScope = _scopeFactory.CreateAsyncScope())
            {
                var context = startupScope.ServiceProvider.GetRequiredService<DataContext>();
                var recoverable = await context.ContentAiGenerationJobs
                    .Where(x => x.Status == "queued" || x.Status == "processing")
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(stoppingToken);
                var staleBefore = DateTime.UtcNow.AddSeconds(-(
                    Math.Clamp(_options.RequestTimeoutSeconds, 30, 600) + 60));
                foreach (var job in recoverable.Where(x => x.Status == "processing"))
                {
                    if (job.UpdatedAt < staleBefore)
                    {
                        job.Status = "failed";
                        job.ErrorCode = "CONTENT_AI_REQUEST_TIMEOUT";
                        job.ErrorMessage = "Article generation timed out. Retry with a shorter topic.";
                    }
                    else
                    {
                        job.Status = "queued";
                    }
                    job.UpdatedAt = DateTime.UtcNow;
                }
                await context.SaveChangesAsync(stoppingToken);
                foreach (var job in recoverable.Where(x => x.Status == "queued"))
                    await _queue.EnqueueAsync(job.Id, stoppingToken);
            }

            var workers = Enumerable.Range(0, Math.Clamp(_options.MaxConcurrentJobs, 1, 4))
                .Select(_ => ProcessQueueAsync(stoppingToken));
            await Task.WhenAll(workers);
        }

        private async Task ProcessQueueAsync(CancellationToken stoppingToken)
        {
            await foreach (var jobId in _queue.ReadAllAsync(stoppingToken))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ContentAiGenerationProcessor>();
                await processor.ProcessAsync(jobId, stoppingToken);
            }
        }
    }
}
