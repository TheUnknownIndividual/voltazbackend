#nullable enable

using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public sealed class MetaInboxMessage
    {
        public long Id { get; set; }
        public int ConversationId { get; set; }
        public MetaInboxConversation Conversation { get; set; } = null!;
        public string? ExternalMessageId { get; set; }
        public MetaInboxMessageDirection Direction { get; set; }
        public string? Text { get; set; }
        public string AttachmentsJson { get; set; } = "[]";
        public int? SentByAdminUserId { get; set; }
        public AdminUser? SentByAdminUser { get; set; }
        public string DeliveryStatus { get; set; } = "received";
        public bool IsHistorical { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
