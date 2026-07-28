namespace Volt.Domain.Entities
{
    public sealed class SolarCalculationLog
    {
        public int Id { get; set; }
        public string Source { get; set; }
        public string EventType { get; set; }
        public int? SolarSalesProjectId { get; set; }
        public SolarSalesProject SolarSalesProject { get; set; }
        public int? AdminUserId { get; set; }
        public AdminUser AdminUser { get; set; }
        public int? AdminTrackedProjectId { get; set; }
        public AdminTrackedProject AdminTrackedProject { get; set; }
        public string Language { get; set; }
        public string SessionId { get; set; }
        public string PayloadJson { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<DocumentLog> DocumentLogs { get; set; } = new List<DocumentLog>();
    }
}
