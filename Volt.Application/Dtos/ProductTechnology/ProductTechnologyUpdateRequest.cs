using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ProductTechnology
{
    public sealed class ProductTechnologyUpdateRequest
    {
        [Required]
        public int ProductCategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }
    }
}
