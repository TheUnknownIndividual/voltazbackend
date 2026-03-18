using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.Step
{
    public class StepUpdateRequest
    {
        [Required]
        public string ImagePath { get; set; }

        [Required]
        public int Position { get; set; }

        [Required]
        [MinLength(1)]
        public List<StepLanguageUpdateRequest> Languages { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
