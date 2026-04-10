 using Volt.Application.Dtos;
using Volt.Application.Dtos.ContactInfo;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IContactInfoService
    {
        Task<ApiResponse<IReadOnlyList<ContactInfoDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ContactInfoDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ContactInfoDto>> CreateAsync(ContactInfoCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<ContactInfoDto>> UpdateAsync(int id, ContactInfoUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}

