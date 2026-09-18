namespace Volt.Domain.Entities
{
    public sealed class ContentAiGenerationJob
    {
        public Guid Id { get; set; }
        public int? CreatedByAdminId { get; set; }
        public string Origin { get; set; } = "admin";
        public string ContentType { get; set; } = string.Empty;
        public int? ContentId { get; set; }
        public string Status { get; set; } = "queued";
        public string RequestJson { get; set; } = string.Empty;
        public string? DraftJson { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
