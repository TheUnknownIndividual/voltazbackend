namespace Volt.Domain.Entities
{
    public sealed class SolarSalesProject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public int? AdminTrackedProjectId { get; set; }
        public AdminTrackedProject AdminTrackedProject { get; set; }

        public ICollection<SolarCalculationLog> CalculationLogs { get; set; } = new List<SolarCalculationLog>();
        public ICollection<DocumentLog> DocumentLogs { get; set; } = new List<DocumentLog>();
    }
}
