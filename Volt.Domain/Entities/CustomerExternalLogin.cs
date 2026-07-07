namespace Volt.Domain.Entities
{
    public class CustomerExternalLogin
    {
        public int Id { get; set; }
        public int CustomerUserId { get; set; }
        public CustomerUser CustomerUser { get; set; }
        public string Provider { get; set; }
        public string ProviderSubject { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
    }
}
