using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.ProdcutCategory
{
    public class ProductCategoryUpdateRequest
    {
        public List<ProductCategoryLanguageUpdateRequest> Languages { get; set; }
    }
}
