using Volt.Application.Dtos;
using Volt.Application.Dtos.ServiceRequest;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IServiceRequestService
    {
        Task<ApiResponse<IReadOnlyList<ServiceRequestDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default);
        Task<ApiResponse<ServiceRequestDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ServiceRequestDto>> CreateAsync(ServiceRequestCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<ServiceRequestDto>> UpdateAsync(int id, ServiceRequestUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<ServiceRequestDto>> UpdateStatusAsync(int id, ServiceRequestStatusUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ServiceRequestDto>> MarkViewedAsync(int id, CancellationToken ct = default);
    }
}
