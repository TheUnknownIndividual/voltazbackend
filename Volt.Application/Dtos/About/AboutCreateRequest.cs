using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.About
{
    public class AboutCreateRequest
    {
        [Required]
        public int Position { get; set; }

        [Required]
        public List<AboutLanguageCreateRequest> Languages { get; set; }

       // public List<IFormFile> ?Images { get; set; }
    }
}
