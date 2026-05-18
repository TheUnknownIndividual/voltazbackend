using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.ProdcutCategory;

namespace Volt.Application.Dtos.Promotion
{
    public class PromotionCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<PromotionLanguageCreateRequest> Languages { get; set; }
    }
}
