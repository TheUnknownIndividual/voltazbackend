namespace Volt.Domain.Entities
{
    public class CustomerPasskeyCredential
    {
        public int Id { get; set; }
        public int CustomerUserId { get; set; }
        public CustomerUser CustomerUser { get; set; }
        public byte[] CredentialId { get; set; }
        public string CredentialIdBase64Url { get; set; }
        public byte[] PublicKey { get; set; }
        public byte[] UserHandle { get; set; }
        public uint SignatureCounter { get; set; }
        public string CredType { get; set; }
        public Guid AaGuid { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
