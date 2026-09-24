using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volt.Application.Common;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services.NewsScraping
{
    public sealed class RenewablesAzNewsScraper : INewsSourceScraper
    {
        private const string Root = "https://renewables.az";

        // Same Azerbaijani month names as the site prints them, folded to plain ASCII capitals before lookup.
        private static readonly Dictionary<string, int> AzMonths = new(StringComparer.Ordinal)
        {
            ["YANVAR"] = 1, ["FEVRAL"] = 2, ["MART"] = 3, ["APREL"] = 4, ["MAY"] = 5, ["IYUN"] = 6,
            ["IYUL"] = 7, ["AVQUST"] = 8, ["SENTYABR"] = 9, ["OKTYABR"] = 10, ["NOYABR"] = 11, ["DEKABR"] = 12,
        };

        // e.g. "21 sentyabr 2026, 17:05 (UTC+4)"
        private static readonly Regex DateRegex = new(
            @"(\d{1,2})\s+([^\W\d_]+)\s+(\d{4}),?\s*(\d{1,2}):(\d{2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RenewableNewsScraperOptions _options;
        private readonly ILogger<RenewablesAzNewsScraper> _logger;
        private readonly HtmlParser _parser = new();

        public RenewablesAzNewsScraper(
            IHttpClientFactory httpClientFactory,
            IOptions<RenewableNewsScraperOptions> options,
            ILogger<RenewablesAzNewsScraper> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public string SourceSite => "renewables";

        public async IAsyncEnumerable<DiscoveredArticleSummary> DiscoverAsync(
            DateTime lookbackCutoffUtc,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("renewable-news-scraper");
            var maxPages = Math.Clamp(_options.MaxPagesPerSource, 1, 50);
            var categories = _options.RenewablesAzCategories
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();

            foreach (var category in categories)
            {
                for (var page = 1; page <= maxPages; page++)
                {
                    ct.ThrowIfCancellationRequested();
                    var pageUrl = page == 1
                        ? $"{Root}/az/category/{category}/news"
                        : $"{Root}/az/category/{category}/news?page={page}";

                    string html;
                    try
                    {
                        html = await RelayFetch.GetStringAsync(client, _options, pageUrl, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Renewables.az discovery failed fetching {PageUrl}", pageUrl);
                        break;
                    }

                    using var document = await _parser.ParseDocumentAsync(html, ct);
                    var items = document.QuerySelectorAll("div.single-around-the-world-news");
                    if (items.Length == 0)
                    {
                        _logger.LogInformation("Renewables.az: 0 items found on {PageUrl}", pageUrl);
                        break;
                    }

                    var reachedCutoff = false;
                    foreach (var item in items)
                    {
                        var link = item.QuerySelector(".news-content h3 a") as IHtmlAnchorElement;
                        var sourceUrl = link?.GetAttribute("href");
                        var title = link?.TextContent?.Trim();
                        if (string.IsNullOrWhiteSpace(sourceUrl) || string.IsNullOrWhiteSpace(title)) continue;

                        if (!TryParseDate(item.QuerySelector(".news-content li")?.TextContent, out var publishedAtUtc))
                            continue;

                        if (publishedAtUtc < lookbackCutoffUtc)
                        {
                            reachedCutoff = true;
                            break;
                        }

                        var imageUrl = item.QuerySelector(".wide-news-image img")?.GetAttribute("src");
                        yield return new DiscoveredArticleSummary(sourceUrl, title, publishedAtUtc, imageUrl);
                    }

                    if (reachedCutoff) break;
                }
            }
        }

        public async Task<ScrapedArticleDetail> FetchDetailAsync(string sourceUrl, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("renewable-news-scraper");
            var html = await RelayFetch.GetStringAsync(client, _options, sourceUrl, ct);
            using var document = await _parser.ParseDocumentAsync(html, ct);

            var title = document.QuerySelector("h1")?.TextContent?.Trim() ?? string.Empty;

            var publishedAtUtc = DateTime.UtcNow;
            var detailText = document.QuerySelector(".news-details")?.TextContent;
            if (TryParseDate(detailText, out var parsedUtc)) publishedAtUtc = parsedUtc;

            // Body paragraphs only: the ads and related-news sit in a separate sidebar column.
            var paragraphs = (document.QuerySelector(".article-content")?.QuerySelectorAll("p")
                              ?? Enumerable.Empty<IElement>())
                .Select(x => x.TextContent.Replace(' ', ' ').Trim())
                .Where(x => x.Length > 0);
            var bodyText = string.Join("\n\n", paragraphs);

            var imageUrls = (document.QuerySelector(".article-img")?.QuerySelectorAll("img") ?? Enumerable.Empty<IElement>())
                .Select(x => x.GetAttribute("src"))
                .Where(src => !string.IsNullOrWhiteSpace(src))
                .Select(src => src!)
                .Distinct()
                .ToList();

            return new ScrapedArticleDetail(title, publishedAtUtc, bodyText, imageUrls);
        }

        private static bool TryParseDate(string? text, out DateTime publishedAtUtc)
        {
            publishedAtUtc = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var match = DateRegex.Match(text);
            if (!match.Success) return false;

            var monthKey = match.Groups[2].Value.Replace('İ', 'I').Replace('ı', 'i').ToUpperInvariant();
            if (!AzMonths.TryGetValue(monthKey, out var month)) return false;

            try
            {
                var local = new DateTime(
                    int.Parse(match.Groups[3].Value), month, int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[4].Value), int.Parse(match.Groups[5].Value), 0, DateTimeKind.Unspecified);
                publishedAtUtc = AzerbaijanClock.ToUtc(local);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }
}
