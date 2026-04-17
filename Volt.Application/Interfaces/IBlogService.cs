using Volt.Application.Dtos;
using Volt.Application.Dtos.Blog;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IBlogService
    {
        Task<ApiResponse<IReadOnlyList<BlogDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> CreateAsync(BlogCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<BlogDto>> UpdateAsync(int id, BlogUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
