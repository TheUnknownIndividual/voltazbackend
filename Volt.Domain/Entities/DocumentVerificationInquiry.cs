namespace Volt.Domain.Entities
{
    public sealed class DocumentVerificationInquiry
    {
        public int Id { get; set; }
        public int DocumentVerificationId { get; set; }
        public DocumentVerification DocumentVerification { get; set; }
        public int? AdminTrackedProjectId { get; set; }
        public AdminTrackedProject AdminTrackedProject { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }
        public string Status { get; set; }
        public int? AssignedAdminUserId { get; set; }
        public AdminUser AssignedAdminUser { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
