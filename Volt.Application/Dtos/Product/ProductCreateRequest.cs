using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Product
{
    public sealed class ProductCreateRequest
    {
        [Required]
        [MaxLength(200)]
        public string ProductName { get; set; }

        [Required]
        public int ProductCategoryId { get; set; }

        [Required]
        public int ProductSubCategoryId { get; set; }

        [Required]
        public int ProductBrandId { get; set; }

        public int? ProductTechnologyId { get; set; }

        public bool InStock { get; set; }

        public bool InHomePage { get; set; }

        [MaxLength(1000)]
        public string Certificate { get; set; }

        public List<string> ProductImage { get; set; } = new();

        public List<string> ProductDatasheet { get; set; } = new();

        public List<ProductParametrCreateRequest> ProductParametrs { get; set; } = new();

        public List<ProductDescriptionCreateRequest> ProductDescriptions { get; set; } = new();

        public List<int> PromotionIds { get; set; } = new();
    }
}
