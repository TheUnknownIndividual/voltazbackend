using Volt.Application.Dtos;
using Volt.Application.Dtos.ProductTechnology;

namespace Volt.Application.Interfaces
{
    public interface IProductTechnologyService
    {
        Task<ApiResponse<ProductTechnologyDto>> CreateAsync(ProductTechnologyCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ProductTechnologyDto>>> GetAllAsync(int productCategoryId, CancellationToken ct = default);
        Task<ApiResponse<ProductTechnologyDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ProductTechnologyDto>> UpdateAsync(int id, ProductTechnologyUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
