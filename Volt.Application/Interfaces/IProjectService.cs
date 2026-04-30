using Volt.Application.Dtos;
using Volt.Application.Dtos.Project;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IProjectService
    {
        Task<ApiResponse<ProjectDto>> CreateAsync(ProjectCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ProjectDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ProjectDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ProjectDto>> UpdateAsync(int id, ProjectUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
