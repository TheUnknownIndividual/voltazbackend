using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectOfferRequest
    {
        public decimal Power { get; set; }
        public byte PowerType { get; set; }

        [Required]
        [MaxLength(120)]
        public string AreaType { get; set; }
    }
}
