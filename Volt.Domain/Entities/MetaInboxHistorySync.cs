#nullable enable

namespace Volt.Domain.Entities
{
    public sealed class MetaInboxHistorySync
    {
        public int Id { get; set; }
        public string PhoneNumberId { get; set; } = string.Empty;
        public string? MetaRequestId { get; set; }
        public string Status { get; set; } = MetaInboxHistorySyncStatuses.Requested;
        public int Progress { get; set; }
        public int? Phase { get; set; }
        public int? LastChunkOrder { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class MetaInboxHistorySyncStatuses
    {
        public const string Requested = "requested";
        public const string Processing = "processing";
        public const string Completed = "completed";
        public const string Declined = "declined";
        public const string Failed = "failed";
    }
}
