using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.ProductBrand
{
    public sealed class ProductBrandCreateRequest
    {
        [Required]
        public int ProductCategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }
    }
}
