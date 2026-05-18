using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ProductCategoryLanguage
    {
        public int Id { get; set; }
        public int ProductCategoryId { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string CategoryName { get; set; }
        public bool IsActive { get; set; }

        public ProductCategory ProductCategory { get; set; }

    }
}
