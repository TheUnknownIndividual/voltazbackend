using System.ComponentModel.DataAnnotations;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Product
{
    public sealed class ProductDescriptionLanguageCreateRequest
    {
        [Required]
        public LanguageCode LanguageCode { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string Features { get; set; }
    }
}
