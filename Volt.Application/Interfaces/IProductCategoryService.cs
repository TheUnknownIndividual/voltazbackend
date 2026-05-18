using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.ProdcutCategory;
using Volt.Application.Dtos;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IProductCategoryService
    {
        Task<ApiResponse<ProductCategoryDto>> CreateAsync(ProductCategoryCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<ProductCategoryDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ProductCategoryDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ProductCategoryDto>> UpdateAsync(int id, ProductCategoryUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
