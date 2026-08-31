#nullable enable

namespace Volt.Application.Dtos.MetaInbox
{
    public sealed record MetaInboxAssigneeDto(int Id, string DisplayName);

    public sealed record MetaInboxConversationDto(
        int Id,
        string Channel,
        string ParticipantExternalId,
        string ParticipantDisplayName,
        string? ParticipantAvatarUrl,
        int? AssignedAdminUserId,
        string? AssignedAdminDisplayName,
        string Status,
        int UnreadCount,
        string LastMessagePreview,
        DateTime LastMessageAt);

    public sealed record MetaInboxConversationPageDto(
        IReadOnlyList<MetaInboxConversationDto> Items,
        int Total,
        int UnreadTotal,
        int Page,
        int PageSize);

    public sealed record MetaInboxAttachmentDto(string Type, string? Url, string? Title);

    public sealed record MetaInboxMessageDto(
        long Id,
        string Direction,
        string? Text,
        IReadOnlyList<MetaInboxAttachmentDto> Attachments,
        int? SentByAdminUserId,
        string? SentByAdminDisplayName,
        string DeliveryStatus,
        DateTime CreatedAt);

    public sealed record MetaInboxInternalNoteDto(
        long Id,
        int AuthorAdminUserId,
        string AuthorDisplayName,
        string Body,
        DateTime CreatedAt);

    public sealed record MetaInboxConfigurationDto(
        bool Enabled,
        bool Ready,
        string GraphApiVersion,
        bool MessengerReady,
        bool WhatsAppReady,
        MetaInboxWebhookDiagnosticsDto Webhook);

    public sealed record MetaInboxWebhookDiagnosticsDto(
        DateTime StartedAtUtc,
        DateTime? LastAttemptAtUtc,
        DateTime? LastCompletedAtUtc,
        DateTime? LastAcceptedAtUtc,
        int? LastResponseStatus,
        string LastResult,
        long AttemptCount,
        long AcceptedCount,
        long RejectedCount,
        long FailedCount,
        string? ObjectType,
        string? Field,
        string? PhoneNumberId,
        int MessageCount,
        int StatusCount);

    public sealed record MetaInboxWebhookProcessingResult(
        string? ObjectType,
        string? Field,
        string? PhoneNumberId,
        int MessageCount,
        int StatusCount);

    public sealed record WhatsAppOnboardingStatusDto(
        bool Configured,
        bool Connected,
        string AppId,
        string ConfigurationId,
        string RedirectUri,
        string WhatsAppBusinessAccountId,
        string PhoneNumberId,
        string? DisplayPhoneNumber,
        string? VerifiedName,
        string? QualityRating,
        string? CodeVerificationStatus,
        string? RuntimeStatus,
        string? PlatformType,
        string? AccountMode,
        bool AppSubscribed,
        bool WebhookMessagesSubscribed,
        string? LastError);

    public sealed record WhatsAppOnboardingCompleteRequest(
        string Code,
        string WhatsAppBusinessAccountId,
        string PhoneNumberId);

    public sealed record WhatsAppOnboardingRegisterRequest(
        string ContinuationToken,
        string Pin);

    public sealed record WhatsAppOnboardingResultDto(
        bool Connected,
        bool RequiresPin,
        string? ContinuationToken,
        WhatsAppOnboardingStatusDto Status);

    public sealed record MetaInboxSendMessageRequest(string Text);
    public sealed record MetaInboxAddNoteRequest(string Body);
    public sealed record MetaInboxAssignRequest(int? AssignedAdminUserId);
    public sealed record MetaInboxStatusRequest(string Status);
}
