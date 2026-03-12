using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class AboutImage
    {
        public int Id { get; set; }
        public int AboutId { get; set; }
        public string ImagePath { get; set; }
        public bool IsActive { get; set; }

        public About About { get; set; }
    }
}
