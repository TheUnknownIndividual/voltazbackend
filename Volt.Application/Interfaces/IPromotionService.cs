using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Promotion;
using Volt.Application.Dtos;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IPromotionService
    {
        Task<ApiResponse<PromotionDto>> CreateAsync(PromotionCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<PromotionDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<PromotionDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<PromotionDto>> UpdateAsync(int id, PromotionUpdateRequest  request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
