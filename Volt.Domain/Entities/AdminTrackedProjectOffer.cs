namespace Volt.Domain.Entities
{
    public sealed class AdminTrackedProjectOffer
    {
        public int Id { get; set; }
        public int AdminTrackedProjectId { get; set; }
        public AdminTrackedProject AdminTrackedProject { get; set; }
        public decimal Power { get; set; }
        public string MountType { get; set; } = string.Empty;
        public string AreaType { get; set; } = string.Empty;
        public decimal ExtraAmount { get; set; }
        public DateTime? SentAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
