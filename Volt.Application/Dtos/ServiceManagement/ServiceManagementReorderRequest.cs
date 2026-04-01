using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.ServiceManagement
{
    public class ServiceManagementReorderRequest
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int Position { get; set; }
    }
}
