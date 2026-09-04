#nullable enable

namespace Volt.Domain.Entities
{
    /// <summary>
    /// A small server-side contact-name cache for WhatsApp Business App coexistence.
    /// Contact state and history chunks can arrive in either order.
    /// </summary>
    public sealed class MetaInboxWhatsAppContact
    {
        public int Id { get; set; }
        public string PhoneNumberId { get; set; } = string.Empty;
        public string ParticipantExternalId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
