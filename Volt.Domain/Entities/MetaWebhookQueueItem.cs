#nullable enable

namespace Volt.Domain.Entities
{
    public sealed class MetaWebhookQueueItem
    {
        public long Id { get; set; }
        public string PayloadHash { get; set; } = string.Empty;
        public string? Payload { get; set; }
        public string Status { get; set; } = MetaWebhookQueueStatuses.Pending;
        public int Attempts { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime NextAttemptAt { get; set; }
        public DateTime? LeaseUntil { get; set; }
        public Guid? LeaseId { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? LastError { get; set; }
    }

    public static class MetaWebhookQueueStatuses
    {
        public const string Pending = "pending";
        public const string Processing = "processing";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }
}
