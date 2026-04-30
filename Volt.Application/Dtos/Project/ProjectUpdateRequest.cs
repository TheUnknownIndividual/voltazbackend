using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectUpdateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ProjectLanguageUpdateRequest> Languages { get; set; }

        public int TotalPower { get; set; }
        public byte PowerType { get; set; }
        public int AnnualProduction { get; set; }
        public byte AnnualProductionType { get; set; }
        public byte SystemType { get; set; }
        public List<string>? NewImagePaths { get; set; }
        public List<int>? DeleteImageIds { get; set; }
    }
}
