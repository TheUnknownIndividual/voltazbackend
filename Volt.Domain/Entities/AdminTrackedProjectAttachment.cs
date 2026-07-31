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
        public string Tag { get; set; } = "Qiymət təklifi";
        public DateTime CreatedAt { get; set; }
        // DOCX text is extracted asynchronously by a bounded background worker.
        // It is retained for the private MCP only; it is never public website data.
        public string? DocumentText { get; set; }
        public DateTime? DocumentExtractedAt { get; set; }
        public string DocumentExtractionStatus { get; set; } = "NotRequired";
        public string? DocumentExtractionError { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
