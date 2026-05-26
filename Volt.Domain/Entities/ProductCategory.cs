using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class ProductCategory
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }

        public ICollection<ProductCategoryLanguage> Languages { get; set; } = new List<ProductCategoryLanguage>();
        public ICollection<ProductBrand> Brands { get; set; } = new List<ProductBrand>();
        public ICollection<ProductTechnology> Technologies { get; set; } = new List<ProductTechnology>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<ProductSubCategory> SubCategories { get; set; } = new List<ProductSubCategory>();
    }
}
