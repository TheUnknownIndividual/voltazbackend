using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.Product
{
    public class ProductFiltrDto
    {
        public int? ProductCategoryId { get; set; }
        public int? ProductSubCategoryId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
