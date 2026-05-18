using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.ProdcutCategory;
using Volt.Application.Dtos;
using Volt.Domain.Enums;
using Volt.Application.Dtos.Promotion;

namespace Volt.Application.Interfaces
{
    public interface IPromotion
    {
        Task<ApiResponse<PromotionDto>> CreateAsync(PromotionCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<PromotionDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<PromotionDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<PromotionDto>> UpdateAsync(int id, PromotionUpdateDto request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
