using Volt.Application.Dtos;
using Volt.Application.Dtos.ProductBrand;

namespace Volt.Application.Interfaces
{
    public interface IProductBrandService
    {
        Task<ApiResponse<ProductBrandDto>> CreateAsync(ProductBrandCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ProductBrandDto>>> GetAllAsync(int productCategoryId, CancellationToken ct = default);
        Task<ApiResponse<ProductBrandDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ProductBrandDto>> UpdateAsync(int id, ProductBrandUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
