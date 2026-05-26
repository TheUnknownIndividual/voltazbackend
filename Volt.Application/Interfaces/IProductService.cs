using Volt.Application.Dtos;
using Volt.Application.Dtos.Product;

namespace Volt.Application.Interfaces
{
    public interface IProductService
    {
        Task<ApiResponse<ProductDto>> CreateAsync(ProductCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ProductDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ProductDto>>> GetAllForHomePageAsync(CancellationToken ct = default);
        Task<ApiResponse<ProductDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ProductDto>> UpdateAsync(int id, ProductUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> ShowHomePage(int id, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
