namespace Volt.Domain.Entities
{
    public class ProjectImage
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ImagePath { get; set; }
        public bool IsActive { get; set; }

        public Project Project { get; set; }
    }
}
