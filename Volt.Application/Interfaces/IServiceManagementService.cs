using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;
using Volt.Application.Dtos;
using Volt.Application.Dtos.ServiceManagement;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IServiceManagementService
    {
        Task<ApiResponse<ServiceManagementDto>> CreateAsync(ServiceManagementCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ServiceManagementDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ServiceManagementDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ServiceManagementDto>> UpdateAsync(int id, ServiceManagementUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> ReorderAsync(List<ServiceManagementReorderRequest> request, CancellationToken ct = default);
    }
}
