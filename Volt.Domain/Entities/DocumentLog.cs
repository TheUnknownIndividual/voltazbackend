namespace Volt.Domain.Entities
{
    public sealed class DocumentLog
    {
        public int Id { get; set; }
        public string DocumentCode { get; set; }
        public string DocumentNumber { get; set; }
        public int SolarSalesProjectId { get; set; }
        public SolarSalesProject SolarSalesProject { get; set; }
        public int? SolarCalculationLogId { get; set; }
        public SolarCalculationLog SolarCalculationLog { get; set; }
        public int? AdminUserId { get; set; }
        public AdminUser AdminUser { get; set; }
        public int? AdminTrackedProjectId { get; set; }
        public AdminTrackedProject AdminTrackedProject { get; set; }
        public string PayloadJson { get; set; }
        public DateTime CreatedAt { get; set; }
        public DocumentVerification Verification { get; set; }
    }
}
