using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectAttachmentRequest
    {
        [Required]
        public string FilePath { get; set; }

        [MaxLength(120)]
        public string? Label { get; set; }
    }
}
