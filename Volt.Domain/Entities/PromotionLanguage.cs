using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class PromotionLanguage
    {
        public int Id { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public int PromotionId { get; set; }
        public string PromotionName { get; set; }
        public bool IsActive { get; set; }

        public Promotion Promotion { get; set; }
    }
}
