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
    public class ServiceManagementLanguageUpdateRequest
    {
        [Required]
        public LanguageCode LanguageCode { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        public string Content1 { get; set; }
        public string Content2 { get; set; }
        public string Content3 { get; set; }
        public string Content4 { get; set; }
        public string DetailContentHtml { get; set; }
        [MaxLength(200)]
        public string SeoTitle { get; set; }
        [MaxLength(500)]
        public string SeoDescription { get; set; }
        [MaxLength(500)]
        public string SeoKeywords { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
