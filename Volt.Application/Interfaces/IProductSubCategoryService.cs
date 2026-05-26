using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.ProdcutCategory;
using Volt.Application.Dtos;
using Volt.Domain.Enums;
using Volt.Application.Dtos.ProductSubCategory;

namespace Volt.Application.Interfaces
{
    public interface IProductSubCategoryService
    {
        Task<ApiResponse<ProductSubCategoryDto>> CreateAsync(ProductSubCategoryCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ProductSubCategoryDto>>> GetAllAsync(int ProductCategoryId , LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ProductSubCategoryDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ProductSubCategoryDto>> UpdateAsync(int id, ProductSubCategoryUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
