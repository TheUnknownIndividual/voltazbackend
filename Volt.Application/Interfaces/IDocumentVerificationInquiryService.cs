using Volt.Application.Dtos;
using Volt.Application.Dtos.Verification;

namespace Volt.Application.Interfaces
{
    public interface IDocumentVerificationInquiryService
    {
        Task<ApiResponse<PublicDocumentVerificationInquiryDto>> CreatePublicAsync(string token, PublicDocumentVerificationInquiryRequest request, CancellationToken ct = default);
        Task<ApiResponse<VerificationInquiryPageDto>> GetPageAsync(int actorAdminUserId, string? type, string? status, int? assignedAdminUserId, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<VerificationInquiryDetailDto>> GetDetailAsync(int id, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<VerificationInquiryDetailDto>> AssignAsync(int id, int? assignedAdminUserId, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<VerificationInquiryDetailDto>> UpdateStatusAsync(int id, string status, int actorAdminUserId, CancellationToken ct = default);
    }
}
