using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.ProdcutCategory;

namespace Volt.Application.Dtos.Promotion
{
    public class PromotionUpdateRequest
    {
        public List<PromotionLanguageUpdateRequest> Languages { get; set; }

    }
}
