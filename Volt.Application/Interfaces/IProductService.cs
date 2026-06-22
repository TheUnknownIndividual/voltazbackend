using Volt.Application.Dtos;
using Volt.Application.Dtos.Product;

namespace Volt.Application.Interfaces
{
    public interface IProductService
    {
        Task<ApiResponse<ProductDto>> CreateAsync(ProductCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<PagedResult<ProductDto>>> GetAllForHomePageAsync(ProductFiltrHomePageDto dto,CancellationToken ct = default);
        Task<ApiResponse<PagedResult<ProductDto>>> GetAllAsync(ProductFiltrDto? dto = null, CancellationToken ct = default);
        Task<ApiResponse<ProductDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ProductDto>> UpdateAsync(int id, ProductUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> ShowHomePage(ProductShowHomePageDto dto, CancellationToken ct = default);
        Task<ApiResponse<int>> ShowHomePageProductCount(CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
