using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.ProdcutCategory;

namespace Volt.Application.Dtos.Promotion
{
    public class PromotionUpdateDto
    {
        public List<PromotionLanguageUpdateDto> Languages { get; set; }

    }
}
