using Volt.Application.Dtos;
using Volt.Application.Dtos.Search;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface ISearchService
    {
        Task<ApiResponse<SearchResultDto>> SearchAsync(
            string query,
            int productLimit,
            LanguageCode? languageCode = null,
            CancellationToken ct = default);
    }
}
