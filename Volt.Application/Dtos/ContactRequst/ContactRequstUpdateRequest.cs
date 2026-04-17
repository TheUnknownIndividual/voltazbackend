using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ContactRequst
{
    public sealed class ContactRequstUpdateRequest
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
        public byte Status { get; set; }

        [Required]
        public int ApplicationTypeId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
