using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.PartnershipRequest
{
    public sealed class PartnershipRequestUpdateRequest
    {
        [Required]
        [MaxLength(200)]
        public string CompanyName { get; set; }

        [Required]
        [MaxLength(150)]
        public string CompanyPerson { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string Email { get; set; }

        [Required]
        [MaxLength(50)]
        public string PhoneNumber { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; }

        [Required]
        public byte Status { get; set; }

        [Required]
        public int PartnershipTypeId { get; set; }
    }
}
