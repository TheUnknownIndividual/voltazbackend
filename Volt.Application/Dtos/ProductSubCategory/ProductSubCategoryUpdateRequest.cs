using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.ProductSubCategory
{
    public class ProductSubCategoryUpdateRequest
    {
        public List<ProductSubCategoryLanguageUpdateRequest> Languages { get; set; }
    }
}
