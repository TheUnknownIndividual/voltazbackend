using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public sealed class ServiceRequest
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Message { get; set; }
        public byte Status { get; set; } //1 - New, 2 - Connected, 3 - Closed
        public int ServiceManagementId { get; set; }
        public ServiceManagement ServiceManagement { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
