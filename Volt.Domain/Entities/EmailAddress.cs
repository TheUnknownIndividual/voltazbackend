using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class EmailAddress
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public int ContactInfoId { get; set; }
        public ContactInfo ContactInfo { get; set; }
    }
}
