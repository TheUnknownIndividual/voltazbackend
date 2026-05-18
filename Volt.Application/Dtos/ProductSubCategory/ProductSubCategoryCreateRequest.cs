using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.ProdcutCategory;

namespace Volt.Application.Dtos.ProductSubCategory
{
    public class ProductSubCategoryCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ProductSubCategoryLanguageCreateRequest> Languages { get; set; }
    }
}
