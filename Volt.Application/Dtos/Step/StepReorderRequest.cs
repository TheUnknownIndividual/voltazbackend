using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.Step
{
    public class StepReorderRequest
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int Position { get; set; }
    }
}
