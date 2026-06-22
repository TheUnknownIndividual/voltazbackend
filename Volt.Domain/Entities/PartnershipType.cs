namespace Volt.Domain.Entities
{
    public class PartnershipType
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<PartnershipTypeLanguage> Languages { get; set; } = new List<PartnershipTypeLanguage>();
    }
}
