using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectUpdateRequest
    {
        [Required]
        public int Position { get; set; }

        [Required]
        [MinLength(1)]
        public List<ProjectLanguageUpdateRequest> Languages { get; set; }

        public List<string>? NewImagePaths { get; set; }
        public List<int>? DeleteImageIds { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
