namespace Volt.Domain.Entities
{
    public sealed class AdminAuditLog
    {
        public long Id { get; set; }
        public int? AdminUserId { get; set; }
        public AdminUser AdminUser { get; set; }
        public string ActorUsername { get; set; }
        public string Action { get; set; }
        public string TargetType { get; set; }
        public string TargetId { get; set; }
        public string Summary { get; set; }
        public bool Succeeded { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
