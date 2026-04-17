using Volt.Application.Dtos;
using Volt.Application.Dtos.NewsPost;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface INewsPostService
    {
        Task<ApiResponse<IReadOnlyList<NewsPostDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NewsPostDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NewsPostDto>> CreateAsync(NewsPostCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NewsPostDto>> UpdateAsync(int id, NewsPostUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
