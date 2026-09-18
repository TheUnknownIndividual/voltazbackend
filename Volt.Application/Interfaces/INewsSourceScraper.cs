namespace Volt.Application.Interfaces
{
    public sealed record DiscoveredArticleSummary(
        string SourceUrl,
        string Title,
        DateTime PublishedAtUtc,
        string? ListingImageUrl);

    public sealed record ScrapedArticleDetail(
        string Title,
        DateTime PublishedAtUtc,
        string BodyText,
        IReadOnlyList<string> ImageUrls);

    public interface INewsSourceScraper
    {
        string SourceSite { get; }

        IAsyncEnumerable<DiscoveredArticleSummary> DiscoverAsync(DateTime lookbackCutoffUtc, CancellationToken ct);

        Task<ScrapedArticleDetail> FetchDetailAsync(string sourceUrl, CancellationToken ct);
    }
}
