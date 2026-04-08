using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class PhoneNumber
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public int ContactInfoId { get; set; }
        public ContactInfo ContactInfo { get; set; }
    }
}
