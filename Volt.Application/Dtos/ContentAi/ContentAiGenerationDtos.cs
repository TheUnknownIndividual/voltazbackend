using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ContentAi
{
    public sealed class ContentAiGenerationStartRequest
    {
        public string ContentType { get; set; } = string.Empty;
        public int? ContentId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string? AngleNotes { get; set; }
        public bool IncludeShillMention { get; set; } = true;
    }

    public sealed record ContentAiLanguageDraftDto(
        LanguageCode LanguageCode,
        string Title,
        string Description,
        string Content,
        string SeoTitle,
        string SeoDescription,
        string SeoKeywords);

    public sealed record ContentAiDraftDto(
        string ContentType,
        IReadOnlyList<ContentAiLanguageDraftDto> Languages,
        IReadOnlyList<string> Warnings,
        bool ShillMentionIncluded);

    public sealed record ContentAiJobDto(
        Guid Id,
        string ContentType,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime ExpiresAt,
        ContentAiDraftDto? Draft,
        string? ErrorCode,
        string? ErrorMessage);

    public sealed record ContentAiSettingsDto(
        bool Enabled,
        string Model,
        int RequestTimeoutSeconds,
        int MinTopicLength,
        int MaxTopicLength,
        int MaxAngleNotesLength);
}
