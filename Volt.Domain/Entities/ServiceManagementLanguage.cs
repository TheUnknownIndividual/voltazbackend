using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ServiceManagementLanguage
    {
        public int Id { get; set; }
        public int ServiceMagamentId { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Content1 { get; set; }
        public string Content2 { get; set; }
        public string Content3 { get; set; }
        public string Content4 { get; set; }
        public bool IsActive { get; set; }

        public ServiceManagement ServiceManagement { get; set; }
    }
}
