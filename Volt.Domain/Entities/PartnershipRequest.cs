namespace Volt.Domain.Entities
{
    public sealed class PartnershipRequest
    {
        public int Id { get; set; }
        public string CompanyName { get; set; }
        public string CompanyPerson { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
        public byte Status { get; set; } // 1 - New, 2 - Connected, 3 - Closed
        public int PartnershipTypeId { get; set; }
        public PartnershipType PartnershipType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
