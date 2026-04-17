using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ServiceRequest
{
    public sealed class ServiceRequestCreateRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(100)]
        public string Surname { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string Email { get; set; }

        [Required]
        [MaxLength(50)]
        public string Phone { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; }

        [Required]
        public int ServiceManagementId { get; set; }
    }
}
