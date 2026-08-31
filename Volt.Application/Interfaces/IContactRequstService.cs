using Volt.Application.Dtos;
using Volt.Application.Dtos.ContactRequst;

namespace Volt.Application.Interfaces
{
    public interface IContactRequstService
    {
        Task<ApiResponse<IReadOnlyList<ContactRequstDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default);
        Task<ApiResponse<ContactRequstDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ContactRequstDto>> CreateAsync(ContactRequstCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<ContactRequstDto>> UpdateAsync(int id, ContactRequstUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<ContactRequstDto>> UpdateStatusAsync(int id, ContactRequstStatusUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<ContactRequstDto>> MarkViewedAsync(int id, CancellationToken ct = default);
    }
}
