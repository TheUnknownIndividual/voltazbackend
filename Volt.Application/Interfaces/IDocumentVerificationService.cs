using Volt.Application.Dtos;
using Volt.Application.Dtos.Verification;

namespace Volt.Application.Interfaces
{
    public interface IDocumentVerificationService
    {
        Task<ApiResponse<IReadOnlyList<AdminDocumentVerificationDto>>> GetRecentAsync(int take = 100, CancellationToken ct = default);
        Task<ApiResponse<PublicDocumentVerificationDto>> GetPublicAsync(string token, CancellationToken ct = default);
        Task<ApiResponse<AdminDocumentVerificationDto>> GetAdminAsync(int documentLogId, CancellationToken ct = default);
        Task<ApiResponse<AdminDocumentVerificationDto>> ReissueAsync(int documentLogId, int adminUserId, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> RevokeAsync(int documentLogId, string reason, int adminUserId, CancellationToken ct = default);
        Task<ApiResponse<AdminDocumentVerificationDto>> LinkProjectAsync(int documentLogId, int adminTrackedProjectId, int adminUserId, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<AdminDocumentVerificationDto>>> GetForProjectAsync(int adminTrackedProjectId, CancellationToken ct = default);
    }
}
