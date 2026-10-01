namespace Volt.Domain.Entities
{
    // Single row (Id = 1): the live LinkedIn Organization OAuth credential, read fresh on every
    // publish/refresh (not cached), so a token rotation takes effect without an app-pool restart.
    // Empty/null token fields mean "not connected yet" -- the admin's one-time Connect flow fills them in.
    public sealed class LinkedInCredential
    {
        public int Id { get; set; }
        public string? OrganizationUrn { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? AccessTokenExpiresAtUtc { get; set; }
        public DateTime? RefreshTokenExpiresAtUtc { get; set; }
        public int? ConnectedByAdminId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
