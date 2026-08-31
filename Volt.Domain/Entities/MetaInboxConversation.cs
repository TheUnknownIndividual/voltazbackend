#nullable enable

using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public sealed class MetaInboxConversation
    {
        public int Id { get; set; }
        public MetaInboxChannel Channel { get; set; }
        public string AccountExternalId { get; set; } = string.Empty;
        public string ParticipantExternalId { get; set; } = string.Empty;
        public string ParticipantDisplayName { get; set; } = string.Empty;
        public string? ParticipantAvatarUrl { get; set; }
        public int? AssignedAdminUserId { get; set; }
        public AdminUser? AssignedAdminUser { get; set; }
        public string Status { get; set; } = "open";
        public int UnreadCount { get; set; }
        public string LastMessagePreview { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<MetaInboxMessage> Messages { get; set; } = new List<MetaInboxMessage>();
        public ICollection<MetaInboxInternalNote> InternalNotes { get; set; } = new List<MetaInboxInternalNote>();
    }
}
