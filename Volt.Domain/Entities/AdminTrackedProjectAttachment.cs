namespace Volt.Domain.Entities
{
    public sealed class AdminTrackedProjectAttachment
    {
        public int Id { get; set; }
        public int AdminTrackedProjectId { get; set; }
        public AdminTrackedProject AdminTrackedProject { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
