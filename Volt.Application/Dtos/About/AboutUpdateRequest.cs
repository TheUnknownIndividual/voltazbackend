using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.About
{
    public class AboutUpdateRequest
    {
        [Required]
        [MinLength(1)]
        public List<AboutLanguageUpdateRequest> Languages { get; set; }

        public string ImagePath { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
