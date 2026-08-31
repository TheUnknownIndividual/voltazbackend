using Volt.Application.Dtos;
using Volt.Application.Dtos.Blog;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IBlogService
    {
        Task<ApiResponse<IReadOnlyList<BlogDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<BlogDto>>> GetAllAdminAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> GetByIdAdminAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> CreateAsync(BlogCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> UpdateAsync(int id, BlogUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> SetStatusAsync(int id, BlogStatusUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
