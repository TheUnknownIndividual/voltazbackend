using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ProductSubCategory
{
    public class ProductSubCategoryLanguageUpdateRequest
    {
        public LanguageCode LanguageCode { get; set; }

        public string SubCategoryName { get; set; }
    }
}
