using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectReorderRequest
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int Position { get; set; }
    }
}
