using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;

namespace Volt.Application.Dtos.ServiceManagement
{
    public class ServiceManagementUpdateRequest
    {

        [Required]
        [MinLength(1)]
        public List<ServiceManagementLanguageUpdateRequest> Languages { get; set; }
        public string Icon { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
