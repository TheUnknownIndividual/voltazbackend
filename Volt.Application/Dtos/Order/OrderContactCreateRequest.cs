using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Order
{
    public sealed class OrderContactCreateRequest
    {
        [Required]
        [MaxLength(160)]
        public string FullName { get; set; }

        [Required]
        [MaxLength(40)]
        public string Phone { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(180)]
        public string Email { get; set; }
    }
}
