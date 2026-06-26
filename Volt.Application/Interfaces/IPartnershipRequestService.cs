using Volt.Application.Dtos;
using Volt.Application.Dtos.PartnershipRequest;

namespace Volt.Application.Interfaces
{
    public interface IPartnershipRequestService
    {
        Task<ApiResponse<IReadOnlyList<PartnershipRequestGetAllDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default);
        Task<ApiResponse<PartnershipRequestDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<PartnershipRequestDto>> CreateAsync(PartnershipRequestCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<PartnershipRequestDto>> UpdateAsync(int id, PartnershipRequestUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<PartnershipRequestDto>> UpdateStatusAsync(int id, PartnershipRequestStatusUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
