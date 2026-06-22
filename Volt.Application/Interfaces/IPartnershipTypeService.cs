using Volt.Application.Dtos;
using Volt.Application.Dtos.PartnershipType;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IPartnershipTypeService
    {
        Task<ApiResponse<IReadOnlyList<PartnershipTypeDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<PartnershipTypeDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<PartnershipTypeDto>> CreateAsync(PartnershipTypeCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<PartnershipTypeDto>> UpdateAsync(int id, PartnershipTypeUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
