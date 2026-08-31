using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ServiceManagement
{
    public class ServiceManagementUpdateRequest
    {

        [Required]
        [MinLength(1)]
        public List<ServiceManagementLanguageUpdateRequest> Languages { get; set; }
        public string Icon { get; set; }
        public ServiceCategory Category { get; set; } = ServiceCategory.Population;
        [MaxLength(500)]
        public string ReadMoreUrl { get; set; }
        [MaxLength(160)]
        public string DetailPageSlug { get; set; }
        [MaxLength(1000)]
        public string BannerImageUrl { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
