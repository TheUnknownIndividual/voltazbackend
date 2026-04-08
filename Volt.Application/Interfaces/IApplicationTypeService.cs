using Volt.Application.Dtos;
using Volt.Application.Dtos.ApplicationType;

namespace Volt.Application.Interfaces
{
    public interface IApplicationTypeService
    {
        Task<ApiResponse<IReadOnlyList<ApplicationTypeDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ApiResponse<ApplicationTypeDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ApplicationTypeDto>> CreateAsync(ApplicationTypeCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<ApplicationTypeDto>> UpdateAsync(int id, ApplicationTypeUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}

