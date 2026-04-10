using Volt.Application.Dtos;
using Volt.Application.Dtos.ApplicationType;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IApplicationTypeService
    {
        Task<ApiResponse<IReadOnlyList<ApplicationTypeDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ApplicationTypeDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ApplicationTypeDto>> CreateAsync(ApplicationTypeCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ApplicationTypeDto>> UpdateAsync(int id, ApplicationTypeUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}

