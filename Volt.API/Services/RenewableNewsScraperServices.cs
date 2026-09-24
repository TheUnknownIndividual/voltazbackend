using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volt.Application.Common;
using Volt.Application.Configuration;
using Volt.Application.Dtos.ContentAi;
using Volt.Application.Dtos.NewsPost;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Infrastructure.Data;
using Volt.Infrastructure.Services.NewsScraping;

namespace Volt.API.Services
{
    internal static class RenewableNewsRelevanceHeuristic
    {
        public static bool IsPlausiblyRelevant(string title, string bodyText, IReadOnlyList<string> keywords)
        {
            if (keywords.Count == 0) return true;
            var haystack = $"{title}\n{bodyText}";
            return keywords.Any(keyword => !string.IsNullOrWhiteSpace(keyword)
                && haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class RenewableNewsScraperRunner
    {
        private static readonly (string Extension, string ContentType)[] AllowedImageTypes =
        {
            (".jpg", "image/jpeg"),
            (".png", "image/png"),
            (".webp", "image/webp"),
        };

        private readonly DataContext _context;
        private readonly IEnumerable<INewsSourceScraper> _scrapers;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IFileService _files;
        private readonly INewsPostService _newsPosts;
        private readonly ContentAiGenerationCoordinator _contentAiCoordinator;
        private readonly ContentAiGenerationProcessor _contentAiProcessor;
        private readonly RenewableNewsScraperOptions _options;
        private readonly ILogger<RenewableNewsScraperRunner> _logger;

        public RenewableNewsScraperRunner(
            DataContext context,
            IEnumerable<INewsSourceScraper> scrapers,
            IHttpClientFactory httpClientFactory,
            IFileService files,
            INewsPostService newsPosts,
            ContentAiGenerationCoordinator contentAiCoordinator,
            ContentAiGenerationProcessor contentAiProcessor,
            IOptions<RenewableNewsScraperOptions> options,
            ILogger<RenewableNewsScraperRunner> logger)
        {
            _context = context;
            _scrapers = scrapers;
            _httpClientFactory = httpClientFactory;
            _files = files;
            _newsPosts = newsPosts;
            _contentAiCoordinator = contentAiCoordinator;
            _contentAiProcessor = contentAiProcessor;
            _options = options.Value;
            _logger = logger;
        }

        // True when a previous run (e.g. cut short by an app-pool recycle) left articles mid-pipeline.
        public Task<bool> HasUnfinishedItemsAsync(CancellationToken ct)
            => _context.ScrapedNewsItems.AnyAsync(x =>
                x.Status == "Discovered" || x.Status == "Detailed" ||
                x.Status == "PendingAiReview" || x.Status == "AiReviewProcessing", ct);

        public async Task ResumeUnfinishedAsync(CancellationToken ct)
        {
            await FetchDetailsAsync(ct);
            await FilterAndRehostAsync(ct);
            await GenerateAndPublishAsync(ct);
        }

        public async Task RunDailyScrapeAsync(CancellationToken ct)
        {
            var lookbackCutoffUtc = DateTime.UtcNow.AddDays(-Math.Max(1, _options.LookbackDays));

            foreach (var scraper in _scrapers)
            {
                await DiscoverAndInsertAsync(scraper, lookbackCutoffUtc, ct);
            }

            await FetchDetailsAsync(ct);
            await FilterAndRehostAsync(ct);
            await GenerateAndPublishAsync(ct);
        }

        private async Task DiscoverAndInsertAsync(INewsSourceScraper scraper, DateTime lookbackCutoffUtc, CancellationToken ct)
        {
            await foreach (var summary in scraper.DiscoverAsync(lookbackCutoffUtc, ct))
            {
                ct.ThrowIfCancellationRequested();
                var exists = await _context.ScrapedNewsItems.AsNoTracking()
                    .AnyAsync(x => x.SourceUrl == summary.SourceUrl, ct);
                if (exists) continue;

                var now = DateTime.UtcNow;
                _context.ScrapedNewsItems.Add(new ScrapedNewsItem
                {
                    SourceSite = scraper.SourceSite,
                    SourceUrl = summary.SourceUrl,
                    SourceTitle = summary.Title,
                    SourcePublishedAt = summary.PublishedAtUtc,
                    SourceListingImageUrl = summary.ListingImageUrl,
                    Status = "Discovered",
                    DiscoveredAt = now,
                    UpdatedAt = now,
                });

                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex)
                {
                    // Lost a race against a concurrent insert of the same SourceUrl -- fine,
                    // it's already recorded. Detach so the failed insert doesn't poison the
                    // change tracker for subsequent saves in this run.
                    _logger.LogInformation(ex, "Duplicate SourceUrl on insert (expected under concurrent runs)");
                    foreach (var entry in _context.ChangeTracker.Entries<ScrapedNewsItem>().ToList())
                        entry.State = EntityState.Detached;
                }
            }
        }

        private async Task FetchDetailsAsync(CancellationToken ct)
        {
            var scrapersBySite = _scrapers.ToDictionary(x => x.SourceSite, x => x);
            var pending = await _context.ScrapedNewsItems
                .Where(x => x.Status == "Discovered")
                .ToListAsync(ct);

            foreach (var item in pending)
            {
                ct.ThrowIfCancellationRequested();
                if (!scrapersBySite.TryGetValue(item.SourceSite, out var scraper))
                {
                    item.Status = "FailedScrape";
                    item.ErrorMessage = $"No scraper registered for source '{item.SourceSite}'.";
                    item.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(ct);
                    continue;
                }

                try
                {
                    var detail = await scraper.FetchDetailAsync(item.SourceUrl, ct);
                    item.RawBodyText = detail.BodyText;
                    item.SourceDetailImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(detail.ImageUrls);
                    if (detail.PublishedAtUtc != default) item.SourcePublishedAt = detail.PublishedAtUtc;
                    item.Status = "Detailed";
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    item.Status = "FailedScrape";
                    item.ErrorMessage = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
                    _logger.LogWarning(ex, "Failed to fetch detail page for {SourceUrl}", item.SourceUrl);
                }
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
        }

        private async Task FilterAndRehostAsync(CancellationToken ct)
        {
            var pending = await _context.ScrapedNewsItems
                .Where(x => x.Status == "Detailed")
                .ToListAsync(ct);

            foreach (var item in pending)
            {
                ct.ThrowIfCancellationRequested();

                var isPlausiblyRelevant = RenewableNewsRelevanceHeuristic.IsPlausiblyRelevant(
                    item.SourceTitle, item.RawBodyText ?? string.Empty, _options.RelevanceKeywords);

                if (isPlausiblyRelevant)
                {
                    var imageUrls = DeserializeImageUrls(item.SourceDetailImageUrlsJson);
                    var candidateImage = imageUrls.FirstOrDefault() ?? item.SourceListingImageUrl;
                    if (!string.IsNullOrWhiteSpace(candidateImage))
                    {
                        item.RehostedImageUrl = await TryRehostImageAsync(candidateImage, ct);
                    }
                }

                item.Status = isPlausiblyRelevant ? "PendingAiReview" : "FilteredKeywordReject";
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
        }

        private async Task GenerateAndPublishAsync(CancellationToken ct)
        {
            var pending = await _context.ScrapedNewsItems
                .Where(x => x.Status == "PendingAiReview" || x.Status == "AiReviewProcessing")
                .ToListAsync(ct);

            foreach (var item in pending)
            {
                ct.ThrowIfCancellationRequested();

                item.Status = "AiReviewProcessing";
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                try
                {
                    var sourceArticle = new ContentAiSourceArticleContext(
                        item.SourceSite, item.SourceUrl, item.SourceTitle,
                        item.RawBodyText ?? string.Empty, item.SourcePublishedAt);
                    var jobId = await _contentAiCoordinator.StartSystemJobAsync("news", sourceArticle, item.Id, ct);
                    await _contentAiProcessor.ProcessAsync(jobId, ct);

                    await _context.Entry(item).ReloadAsync(ct);
                    if (item.Status == "DraftReady")
                    {
                        await PublishAsync(item, jobId, ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Renewable news AI generation failed for {SourceUrl}", item.SourceUrl);
                    item.Status = "FailedAi";
                    item.ErrorMessage = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
                    item.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.InterArticleDelaySeconds)), ct);
            }
        }

        private async Task PublishAsync(ScrapedNewsItem item, Guid jobId, CancellationToken ct)
        {
            var job = await _context.ContentAiGenerationJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId, ct);
            if (job?.DraftJson is null)
            {
                item.Status = "FailedPublish";
                item.ErrorMessage = "AI draft was missing at publish time.";
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return;
            }

            var draft = System.Text.Json.JsonSerializer.Deserialize<ContentAiDraftDto>(
                job.DraftJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (draft is null || draft.Languages.Count == 0)
            {
                item.Status = "FailedPublish";
                item.ErrorMessage = "AI draft could not be parsed at publish time.";
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return;
            }

            var coverImage = item.RehostedImageUrl
                ?? DeserializeImageUrls(item.SourceDetailImageUrlsJson).FirstOrDefault()
                ?? item.SourceListingImageUrl;
            if (string.IsNullOrWhiteSpace(coverImage))
            {
                item.Status = "FailedPublish";
                item.ErrorMessage = "No cover image was available (required to publish a News post).";
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return;
            }

            var sourceLabel = item.SourceSite switch
            {
                "minenergy" => "Energetika Nazirliyi",
                "renewables" => "Renewables.az",
                _ => "Bərpa Olunan Enerji Mənbələri üzrə Dövlət Agentliyi",
            };

            var request = new NewsPostCreateRequest
            {
                CoverImagePath = coverImage,
                CoverImagePositionX = 50,
                CoverImagePositionY = 50,
                CoverImageZoom = 1m,
                Source = sourceLabel,
                PostLink = item.SourceUrl,
                Languages = draft.Languages.Select(language => new NewsPostLanguageCreateRequest
                {
                    LanguageCode = language.LanguageCode,
                    Title = language.Title,
                    Description = language.Description,
                    Content = language.Content,
                    SeoTitle = language.SeoTitle,
                    SeoDescription = language.SeoDescription,
                    SeoKeywords = language.SeoKeywords,
                }).ToList(),
            };

            var result = await _newsPosts.CreateAsync(request, LanguageCode.AZ, ct);
            if (!result.Success || result.Data is null)
            {
                item.Status = "FailedPublish";
                item.ErrorMessage = result.Error?.Code ?? "News post creation failed.";
                item.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return;
            }

            item.Status = "Published";
            item.PublishedContentType = "news";
            item.PublishedContentId = result.Data.Id;
            item.PublishedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        private async Task<string?> TryRehostImageAsync(string remoteImageUrl, CancellationToken ct)
        {
            try
            {
                if (!Uri.TryCreate(remoteImageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    return null;

                var client = _httpClientFactory.CreateClient("renewable-news-scraper");
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.ImageDownloadTimeoutSeconds, 5, 60)));

                using var response = await RelayFetch.GetResponseAsync(client, _options, uri, timeoutCts.Token);
                if (!response.IsSuccessStatusCode) return null;

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                var matchedType = AllowedImageTypes.FirstOrDefault(x => string.Equals(x.ContentType, mediaType, StringComparison.OrdinalIgnoreCase));
                if (matchedType == default) return null;

                var maxBytes = Math.Clamp(_options.MaxImageBytes, 1024, 50 * 1024 * 1024);
                if (response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > maxBytes)
                    return null;

                await using var remoteStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await remoteStream.ReadAsync(chunk, timeoutCts.Token)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > maxBytes) return null;
                }

                buffer.Position = 0;
                var header = new byte[12];
                var headerRead = await buffer.ReadAsync(header.AsMemory(0, Math.Min(12, (int)buffer.Length)), ct);
                buffer.Position = 0;
                if (!MatchesSignature(matchedType.Extension, header.AsSpan(0, headerRead))) return null;

                var fileName = $"scraped{matchedType.Extension}";
                var uploaded = await _files.UploadImageAsync(
                    new Volt.Application.Dtos.FileUploadRequest { FileName = fileName, Content = buffer },
                    "scraped-news",
                    ct);
                return uploaded;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Timed out rehosting image {RemoteImageUrl}", remoteImageUrl);
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to rehost image {RemoteImageUrl}", remoteImageUrl);
                return null;
            }
        }

        private static bool MatchesSignature(string extension, ReadOnlySpan<byte> header) => extension switch
        {
            ".jpg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false,
        };

        private static IReadOnlyList<string> DeserializeImageUrls(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    public sealed class RenewableNewsScraperBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RenewableNewsScraperOptions _options;
        private readonly ILogger<RenewableNewsScraperBackgroundService> _logger;

        public RenewableNewsScraperBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptions<RenewableNewsScraperOptions> options,
            ILogger<RenewableNewsScraperBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<DataContext>();

                    if (_options.Enabled)
                    {
                        var nowBaku = AzerbaijanClock.Now;
                        var runState = await context.RenewableNewsScraperRunState.FirstOrDefaultAsync(x => x.Id == 1, stoppingToken);
                        var today = DateOnly.FromDateTime(nowBaku);
                        var shouldRun = nowBaku.Hour >= _options.RunHourLocal && (runState is null || runState.LastRunDateUtc != today);

                        _logger.LogInformation(
                            "RenewableNewsScraper gate check: nowBaku={NowBaku} runHourLocal={RunHourLocal} lastRunDateUtc={LastRunDateUtc} willRun={WillRun}",
                            nowBaku, _options.RunHourLocal, runState?.LastRunDateUtc, shouldRun);

                        if (shouldRun)
                        {
                            if (runState is null)
                            {
                                runState = new RenewableNewsScraperRunState { Id = 1 };
                                context.RenewableNewsScraperRunState.Add(runState);
                            }
                            runState.LastRunDateUtc = today;
                            runState.UpdatedAt = DateTime.UtcNow;
                            await context.SaveChangesAsync(stoppingToken);

                            var runner = scope.ServiceProvider.GetRequiredService<RenewableNewsScraperRunner>();
                            await runner.RunDailyScrapeAsync(stoppingToken);
                        }
                        else if (runState is not null && runState.LastRunDateUtc == today)
                        {
                            var runner = scope.ServiceProvider.GetRequiredService<RenewableNewsScraperRunner>();
                            if (await runner.HasUnfinishedItemsAsync(stoppingToken))
                            {
                                _logger.LogInformation("RenewableNewsScraper resuming unfinished articles from today's run");
                                await runner.ResumeUnfinishedAsync(stoppingToken);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Renewable news scrape run failed");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
