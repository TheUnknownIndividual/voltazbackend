using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ServiceManagement
    {
        public int Id { get; set; }
        public string Icon { get; set; }
        public bool IsActive { get; set; }
        public ServiceCategory Category { get; set; } = ServiceCategory.Population;
        public string ReadMoreUrl { get; set; }
        public string DetailPageSlug { get; set; }
        public string BannerImageUrl { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ServiceManagementLanguage> Languages { get; set; } = new List<ServiceManagementLanguage>();
        public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    }
}
