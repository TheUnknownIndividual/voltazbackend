using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public sealed class AuthRefreshToken
    {
        public Guid Id { get; set; }
        public string TokenHash { get; set; }
        public Role Role { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        /// <summary>
        /// When this login session first began. Preserved across every rotation of the
        /// same session so an absolute session-length cap can be enforced even while the
        /// user stays active and the token keeps rotating.
        /// </summary>
        public DateTime SessionStartedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public Guid? ReplacedByTokenId { get; set; }
        public byte[] ConcurrencyToken { get; set; }
    }
}
