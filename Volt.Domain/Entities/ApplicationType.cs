using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Domain.Entities
{
    public class ApplicationType
    {
        public int Id { get; set; }
        public int? ServiceManagementId { get; set; }
        public ServiceManagement ServiceManagement { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<ApplicationTypeLanguage> Languages { get; set; } = new List<ApplicationTypeLanguage>();
        public ICollection<ContactRequst> ContactRequsts { get; set; } = new List<ContactRequst>();
    }
}
