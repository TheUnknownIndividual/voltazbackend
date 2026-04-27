using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class About
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<AboutLanguage> Languages { get; set; } = new List<AboutLanguage>();
        public ICollection<AboutImage> Images { get; set; } = new List<AboutImage>();
    }
}
