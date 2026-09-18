using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;
using Volt.Application.Common;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services.NewsScraping
{
    public sealed class AreaGovNewsScraper : INewsSourceScraper
    {
        private const string BaseUrl = "https://area.gov.az/az/page/media/news";
        // Keys are normalized (uppercase, Azerbaijani dotted/dotless I folded to plain ASCII I)
        // before lookup -- see NormalizeMonthToken -- so this dictionary uses an exact ordinal
        // comparer rather than relying on OrdinalIgnoreCase's handling of non-ASCII casing.
        private static readonly Dictionary<string, int> AzMonths = new(StringComparer.Ordinal)
        {
            ["YANVAR"] = 1, ["FEVRAL"] = 2, ["MART"] = 3, ["APREL"] = 4,
            ["MAY"] = 5, ["IYUN"] = 6, ["IYUL"] = 7,
            ["AVQUST"] = 8, ["SENTYABR"] = 9, ["OKTYABR"] = 10, ["NOYABR"] = 11, ["DEKABR"] = 12,
        };

        private static string NormalizeMonthToken(string token)
            => token.Replace('İ', 'I').Replace('ı', 'i').ToUpperInvariant();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RenewableNewsScraperOptions _options;
        private readonly HtmlParser _parser = new();

        public AreaGovNewsScraper(IHttpClientFactory httpClientFactory, IOptions<RenewableNewsScraperOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public string SourceSite => "area";

        public async IAsyncEnumerable<DiscoveredArticleSummary> DiscoverAsync(
            DateTime lookbackCutoffUtc,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("renewable-news-scraper");
            var maxPages = Math.Clamp(_options.MaxPagesPerSource, 1, 50);

            for (var page = 1; page <= maxPages; page++)
            {
                ct.ThrowIfCancellationRequested();
                var pageUrl = page == 1 ? BaseUrl : $"{BaseUrl}?page={page}";

                string html;
                try
                {
                    html = await client.GetStringAsync(pageUrl, ct);
                }
                catch
                {
                    yield break;
                }

                using var document = await _parser.ParseDocumentAsync(html, ct);
                var items = document.QuerySelectorAll("a.card");
                if (items.Length == 0) yield break;

                var reachedCutoff = false;
                foreach (var item in items)
                {
                    if (item is not IHtmlAnchorElement anchor) continue;
                    var sourceUrl = anchor.GetAttribute("href");
                    var title = item.QuerySelector("p")?.TextContent?.Trim();
                    if (string.IsNullOrWhiteSpace(sourceUrl) || string.IsNullOrWhiteSpace(title)) continue;

                    var dateText = item.QuerySelector("time")?.TextContent?.Trim();
                    if (!TryParseAzDate(dateText, out var publishedAtUtc))
                    {
                        continue;
                    }

                    if (publishedAtUtc < lookbackCutoffUtc)
                    {
                        reachedCutoff = true;
                        break;
                    }

                    var imageUrl = item.QuerySelector("img.image")?.GetAttribute("src");
                    yield return new DiscoveredArticleSummary(sourceUrl, title, publishedAtUtc, imageUrl);
                }

                if (reachedCutoff) yield break;
            }
        }

        public async Task<ScrapedArticleDetail> FetchDetailAsync(string sourceUrl, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("renewable-news-scraper");
            var html = await client.GetStringAsync(sourceUrl, ct);
            using var document = await _parser.ParseDocumentAsync(html, ct);

            var title = document.QuerySelector("div.title")?.TextContent?.Trim() ?? string.Empty;

            var publishedAtUtc = DateTime.UtcNow;
            var dateText = document.QuerySelector("time")?.TextContent?.Trim();
            if (TryParseAzDate(dateText, out var parsedUtc))
            {
                publishedAtUtc = parsedUtc;
            }

            var bodyContainer = document.QuerySelector("div.col-12.content-body");
            var bodyText = bodyContainer?.TextContent?.Trim() ?? string.Empty;
            var imageUrls = (bodyContainer?.QuerySelectorAll("img") ?? Enumerable.Empty<IElement>())
                .Select(x => x.GetAttribute("src"))
                .Where(src => !string.IsNullOrWhiteSpace(src))
                .Select(src => src!)
                .Distinct()
                .ToList();

            return new ScrapedArticleDetail(title, publishedAtUtc, bodyText, imageUrls);
        }

        // Site renders dates as e.g. "08 SENTYABR 2026" -- Azerbaijani month names, not
        // reliably parseable via CultureInfo, so a small explicit lookup is used instead.
        private static bool TryParseAzDate(string? text, out DateTime publishedAtUtc)
        {
            publishedAtUtc = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out var day)) return false;
            if (!AzMonths.TryGetValue(NormalizeMonthToken(parts[1]), out var month)) return false;
            if (!int.TryParse(parts[2], out var year)) return false;

            try
            {
                var local = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
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
