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
        public DateTime? RevokedAt { get; set; }
        public Guid? ReplacedByTokenId { get; set; }
        public byte[] ConcurrencyToken { get; set; }
    }
}
