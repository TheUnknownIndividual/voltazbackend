namespace Volt.Domain.Entities
{
    public class Project
    {
        public int Id { get; set; }
        public int TotalPower { get; set; }
        public byte PowerType { get; set; }
        public int AnnualProduction { get; set; }
        public byte AnnualProductionType { get; set; }
        public byte SystemType { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ProjectLanguage> Languages { get; set; } = new List<ProjectLanguage>();
        public ICollection<ProjectImage> Images { get; set; } = new List<ProjectImage>();
    }
}
