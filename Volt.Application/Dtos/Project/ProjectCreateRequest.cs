using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ProjectLanguageCreateRequest> Languages { get; set; }

        public List<string>? ImagePaths { get; set; }
    }
}
