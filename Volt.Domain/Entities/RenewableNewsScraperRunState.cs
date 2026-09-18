namespace Volt.Domain.Entities
{
    // Single fixed-row table (Id = 1) tracking whether today's daily scrape run has
    // already started, independent of any per-item ScrapedNewsItem state so it's
    // correct even on a day with zero newly discovered articles.
    public sealed class RenewableNewsScraperRunState
    {
        public int Id { get; set; }
        public DateOnly? LastRunDateUtc { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
