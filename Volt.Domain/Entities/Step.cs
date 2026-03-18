using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class Step
    {
        public int Id { get; set; }
        public string ImagePath { get; set; }
        public int Position { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<StepLanguage> Languages { get; set; } = new List<StepLanguage>();
    }
}
