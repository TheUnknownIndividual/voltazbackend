using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ApplicationTypeLanguage
    {
        public int Id { get; set; }
        public int ApplicationTypeId { get; set; }
        public ApplicationType ApplicationType { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }
}
