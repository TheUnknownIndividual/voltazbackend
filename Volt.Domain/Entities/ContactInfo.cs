using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class ContactInfo
    {
        public int Id { get; set; }
        public ICollection<PhoneNumber> PhoneNumbers { get; set; }
        public ICollection<EmailAddress> EmailAddresses { get; set; }
        public ICollection<ContactLanguage> Languages{ get; set; }
    }
}
