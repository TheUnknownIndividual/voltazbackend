#nullable enable

namespace Volt.Domain.Entities
{
    public sealed class MetaInboxInternalNote
    {
        public long Id { get; set; }
        public int ConversationId { get; set; }
        public MetaInboxConversation Conversation { get; set; } = null!;
        public int AuthorAdminUserId { get; set; }
        public AdminUser AuthorAdminUser { get; set; } = null!;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
