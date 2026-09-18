using System.Globalization;
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
    public sealed class MinenergyNewsScraper : INewsSourceScraper
    {
        private const string BaseUrl = "https://minenergy.gov.az/az/xeberler-arxivi";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RenewableNewsScraperOptions _options;
        private readonly HtmlParser _parser = new();

        public MinenergyNewsScraper(IHttpClientFactory httpClientFactory, IOptions<RenewableNewsScraperOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public string SourceSite => "minenergy";

        public async IAsyncEnumerable<DiscoveredArticleSummary> DiscoverAsync(
            DateTime lookbackCutoffUtc,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("renewable-news-scraper");
            var maxPages = Math.Clamp(_options.MaxPagesPerSource, 1, 50);

            for (var page = 1; page <= maxPages; page++)
            {
                ct.ThrowIfCancellationRequested();
                var pageUrl = page == 1 ? BaseUrl : $"{BaseUrl}/page/{page}";

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
                var items = document.QuerySelectorAll("article.Post-item");
                if (items.Length == 0) yield break;

                var reachedCutoff = false;
                foreach (var item in items)
                {
                    var link = item.QuerySelector("h3.Post-item-content__title a") as IHtmlAnchorElement
                        ?? item.QuerySelector("a") as IHtmlAnchorElement;
                    var sourceUrl = link?.GetAttribute("href");
                    var title = link?.TextContent?.Trim();
                    if (string.IsNullOrWhiteSpace(sourceUrl) || string.IsNullOrWhiteSpace(title)) continue;

                    var dateText = item.QuerySelector("time.Post-item-content__date")?.TextContent?.Trim();
                    if (!TryParseListingDate(dateText, out var publishedAtUtc))
                    {
                        continue;
                    }

                    if (publishedAtUtc < lookbackCutoffUtc)
                    {
                        reachedCutoff = true;
                        break;
                    }

                    var imageUrl = item.QuerySelector("figure.Post-item__image img")?.GetAttribute("src");
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

            var title = document.QuerySelector("h1.Page-header__title")?.TextContent?.Trim() ?? string.Empty;

            var publishedAtUtc = DateTime.UtcNow;
            var timeElement = document.QuerySelector("time.Page-header__date");
            var datetimeAttr = timeElement?.GetAttribute("datetime");
            if (!string.IsNullOrWhiteSpace(datetimeAttr) &&
                DateTime.TryParse(datetimeAttr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedLocal))
            {
                publishedAtUtc = AzerbaijanClock.ToUtc(parsedLocal);
            }

            var bodyContainer = document.QuerySelector("#fullstoryPrint");
            var bodyText = bodyContainer?.TextContent?.Trim() ?? string.Empty;
            var imageUrls = (bodyContainer?.QuerySelectorAll("img") ?? Enumerable.Empty<IElement>())
                .Select(x => x.GetAttribute("src"))
                .Where(src => !string.IsNullOrWhiteSpace(src))
                .Select(src => src!)
                .Distinct()
                .ToList();

            return new ScrapedArticleDetail(title, publishedAtUtc, bodyText, imageUrls);
        }

        private static bool TryParseListingDate(string? text, out DateTime publishedAtUtc)
        {
            publishedAtUtc = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (!DateTime.TryParseExact(
                    text.Trim(), "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return false;
            }
            publishedAtUtc = AzerbaijanClock.ToUtc(local);
            return true;
        }
    }
}
