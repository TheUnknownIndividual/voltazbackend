using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ContactLanguage
    {
        public int Id { get; set; }
        public LanguageCode LanguageCode { get; set; }

        public string Address { get; set; }
        public string WorkingHoursDescription { get; set; }

        public int ContactInfoId { get; set; }
        public ContactInfo ContactInfo { get; set; }
    }
}
