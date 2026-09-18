namespace Volt.Domain.Entities
{
    public sealed class ScrapedNewsItem
    {
        public int Id { get; set; }
        public string SourceSite { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string SourceTitle { get; set; } = string.Empty;
        public DateTime SourcePublishedAt { get; set; }
        public string? SourceListingImageUrl { get; set; }
        public string? SourceDetailImageUrlsJson { get; set; }
        public string? RawBodyText { get; set; }
        public string? RehostedImageUrl { get; set; }

        public string Status { get; set; } = "Discovered";

        public Guid? ContentAiGenerationJobId { get; set; }
        public string? RelevanceReason { get; set; }

        public string? PublishedContentType { get; set; }
        public int? PublishedContentId { get; set; }
        public DateTime? PublishedAt { get; set; }

        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTime DiscoveredAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
