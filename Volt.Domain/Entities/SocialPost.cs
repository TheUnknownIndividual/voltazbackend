namespace Volt.Domain.Entities
{
    // One row per (platform, source item). The unique index is the hard idempotency guard:
    // the same news article or product can never be posted twice on the same platform.
    // Deliberately has no foreign keys so it can never affect news/product deletes.
    public sealed class SocialPost
    {
        public int Id { get; set; }
        // "news" | "product"
        public string SourceType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        // "facebook" | "instagram"
        public string Platform { get; set; } = string.Empty;
        // candidate | rejected | dryrun | publishing | published | failed
        public string Status { get; set; } = "candidate";
        public string? Caption { get; set; }
        public string? ImageUrl { get; set; }
        public string? LinkUrl { get; set; }
        public string? TopicKey { get; set; }
        public int? QualityScore { get; set; }
        public string? RejectReason { get; set; }
        public string? ExternalId { get; set; }
        public string? PermalinkUrl { get; set; }
        public int Attempts { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PostedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Single row (Id = 1): lets an admin pause automatic posting instantly, without a deploy.
    public sealed class SocialPostingState
    {
        public int Id { get; set; }
        public bool Paused { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
