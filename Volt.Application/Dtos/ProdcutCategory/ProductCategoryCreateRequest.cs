using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.ProdcutCategory
{
    public class ProductCategoryCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ProductCategoryLanguageCreateRequest> Languages { get; set; }
    }
}
