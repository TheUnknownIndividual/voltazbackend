using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class Promotion
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }

        public ICollection<PromotionLanguage> Languages { get; set; } = new List<PromotionLanguage>();

    }
}
