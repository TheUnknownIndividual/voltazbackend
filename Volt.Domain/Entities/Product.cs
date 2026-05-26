namespace Volt.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string ProductName { get; set; }
        public int ProductCategoryId { get; set; }
        public ProductCategory ProductCategory { get; set; }
        public int ProductSubCategoryId { get; set; }
        public ProductSubCategory ProductSubCategory { get; set; }
        public int ProductBrandId { get; set; }
        public ProductBrand ProductBrand { get; set; }
        public int? ProductTechnologyId { get; set; }
        public ProductTechnology ProductTechnology { get; set; }
        public bool InStock { get; set; }
        public bool InHomePage { get; set; }
        public string Certificate { get; set; }
        public bool IsActive { get; set; }

        public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
        public ICollection<ProductParametr> ProductParametrs { get; set; } = new List<ProductParametr>();
        public ICollection<ProductDescription> ProductDescriptions { get; set; } = new List<ProductDescription>();
        public ICollection<ProductPromotion> ProductPromotions { get; set; } = new List<ProductPromotion>();
    }
}
