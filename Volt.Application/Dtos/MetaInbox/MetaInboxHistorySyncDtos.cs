#nullable enable

namespace Volt.Application.Dtos.MetaInbox
{
    public sealed record MetaInboxHistorySyncDto(
        string PhoneNumberId,
        string? MetaRequestId,
        string Status,
        int Progress,
        int? Phase,
        int? LastChunkOrder,
        DateTime? RequestedAt,
        DateTime? CompletedAt,
        DateTime? UpdatedAt,
        string? ErrorCode,
        string? ErrorMessage);

    public sealed record MetaInboxHistorySyncProgressUpdate(
        string PhoneNumberId,
        int Progress,
        int? Phase,
        int? ChunkOrder,
        DateTime ObservedAt);

    public sealed record MetaInboxHistorySyncFailureUpdate(
        string PhoneNumberId,
        string Status,
        string ErrorCode,
        string ErrorMessage,
        DateTime ObservedAt);
}
