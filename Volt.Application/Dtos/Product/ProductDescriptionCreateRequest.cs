using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Product
{
    public sealed class ProductDescriptionCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ProductDescriptionLanguageCreateRequest> Languages { get; set; } = new();
    }
}
