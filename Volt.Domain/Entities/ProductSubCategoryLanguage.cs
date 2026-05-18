using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ProductSubCategoryLanguage
    {
        public int Id { get; set; }
        public int ProductSubCategoryId { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string SubCategoryName { get; set; }
        public bool IsActive { get; set; }

        public ProductSubCategory ProductSubCategory { get; set; }
    }
}
