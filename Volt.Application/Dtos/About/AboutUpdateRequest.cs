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
        public int Position { get; set; }

        [Required]
        [MinLength(1)]
        public List<AboutLanguageUpdateRequest> Languages { get; set; }

        public List<string>? NewImagePaths { get; set; }
        public List<int>? DeleteImageIds { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
