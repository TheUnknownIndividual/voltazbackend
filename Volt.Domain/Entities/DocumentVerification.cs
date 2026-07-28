namespace Volt.Domain.Entities
{
    public sealed class DocumentVerification
    {
        public int Id { get; set; }
        public int DocumentLogId { get; set; }
        public DocumentLog DocumentLog { get; set; }
        public string PublicToken { get; set; }
        public string DocumentNumber { get; set; }
        public string DocumentCode { get; set; }
        public string IssuerDisplayName { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string RevocationReason { get; set; }
        public int? RevokedByAdminUserId { get; set; }
        public AdminUser RevokedByAdminUser { get; set; }
        public ICollection<DocumentVerificationInquiry> Inquiries { get; set; } = new List<DocumentVerificationInquiry>();
    }
}
