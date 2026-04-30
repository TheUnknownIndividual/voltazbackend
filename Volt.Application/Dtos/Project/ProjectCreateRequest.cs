using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectCreateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ProjectLanguageCreateRequest> Languages { get; set; }

        public List<string>? ImagePaths { get; set; }

        public int TotalPower { get; set; }
        public byte PowerType { get; set; }
        public int AnnualProduction { get; set; }
        public byte AnnualProductionType { get; set; }
        public byte SystemType { get; set; }
    }
}
