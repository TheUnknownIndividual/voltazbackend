using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ProjectLanguage
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public bool IsActive { get; set; }

        public Project Project { get; set; }
    }
}
