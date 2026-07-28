using Volt.Application.Dtos;
using Volt.Application.Dtos.AdminProjectTracker;

namespace Volt.Application.Interfaces
{
    public interface IAdminProjectTrackerService
    {
        Task<ApiResponse<IReadOnlyList<AdminTrackedProjectDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ApiResponse<AdminTrackedProjectDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<AdminTrackedProjectDto>> CreateAsync(AdminTrackedProjectUpsertRequest request, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<AdminTrackedProjectDto>> UpdateAsync(int id, AdminTrackedProjectUpsertRequest request, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<AdminTrackedProjectDto>> RequestStakeholderReviewAsync(int id, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<AdminTrackedProjectDto>> RetryStakeholderApprovalAsync(int id, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<AdminTrackedProjectDto>> RecordStakeholderDecisionAsync(int requestId, long telegramChatId, bool approved, CancellationToken ct = default);
        Task<ApiResponse<AdminTrackedProjectDto>> AddAttachmentAsync(int id, AdminTrackedProjectAttachmentRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
