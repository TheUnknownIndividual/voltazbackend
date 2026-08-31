using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.ProdcutCategory
{
    public class ProductCategoryUpdateRequest
    {
        public List<ProductCategoryLanguageUpdateRequest> Languages { get; set; }

        public bool ShowOnHomePage { get; set; }

        [Range(0, 5)]
        public int HomePageDisplayOrder { get; set; }

        public int? HomePageProductId { get; set; }
    }
}
