#nullable enable

using Volt.Application.Dtos;
using Volt.Application.Dtos.MetaInbox;

namespace Volt.Application.Interfaces
{
    public interface IMetaInboxService
    {
        Task<MetaInboxWebhookProcessingResult> ProcessWebhookAsync(string payload, CancellationToken ct = default);
        Task<bool> CanViewAllConversationsAsync(int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<MetaInboxConversationPageDto>> GetConversationsAsync(string? search, string? status, string? assignment, int actorAdminUserId, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<MetaInboxMessageDto>>> GetMessagesAsync(int conversationId, long? afterId, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<MetaInboxInternalNoteDto>>> GetNotesAsync(int conversationId, long? afterId, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<MetaInboxAssigneeDto>>> GetAssigneesAsync(int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<int>> GetUnreadCountAsync(int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<MetaInboxConversationDto>> AssignAsync(int conversationId, int? assignedAdminUserId, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<MetaInboxConversationDto>> UpdateStatusAsync(int conversationId, string status, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<MetaInboxConversationDto>> MarkReadAsync(int conversationId, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<MetaInboxMessageDto>> SendMessageAsync(int conversationId, string text, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<MetaInboxMessageDto>> SendAttachmentAsync(int conversationId, FileUploadRequest file, string? caption, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<MetaInboxInternalNoteDto>> AddNoteAsync(int conversationId, string body, int actorAdminUserId, CancellationToken ct = default);
    }

    public interface IMetaWhatsAppOnboardingService
    {
        Task<WhatsAppOnboardingStatusDto> GetStatusAsync(CancellationToken ct = default);
        Task<ApiResponse<WhatsAppOnboardingResultDto>> CompleteAsync(WhatsAppOnboardingCompleteRequest request, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<WhatsAppOnboardingResultDto>> RegisterAsync(WhatsAppOnboardingRegisterRequest request, int actorAdminUserId, CancellationToken ct = default);
    }
}
