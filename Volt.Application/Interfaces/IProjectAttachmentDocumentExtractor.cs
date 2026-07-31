namespace Volt.Application.Interfaces;

public sealed record ProjectAttachmentDocumentExtractionResult(bool Succeeded, string Text, string? FailureReason);

public interface IProjectAttachmentDocumentExtractor
{
    Task<ProjectAttachmentDocumentExtractionResult> ExtractDocxTextAsync(string fileUrl, CancellationToken ct = default);
}
