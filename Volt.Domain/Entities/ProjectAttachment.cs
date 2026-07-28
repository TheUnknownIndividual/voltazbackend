namespace Volt.Domain.Entities
{
    public class ProjectAttachment
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string FilePath { get; set; }
        public string? Label { get; set; }
        public bool IsActive { get; set; }

        public Project Project { get; set; }
    }
}
