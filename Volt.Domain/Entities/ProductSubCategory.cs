using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class ProductSubCategory
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }

        public ICollection<ProductSubCategoryLanguage> Languages { get; set; } = new List<ProductSubCategoryLanguage>();
    }
}
