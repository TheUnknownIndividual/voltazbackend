using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.About;
using Volt.Application.Dtos;

namespace Volt.Application.Interfaces
{
    public interface IAboutService
    {
        Task<ApiResponse<AboutDto>> CreateAsync(AboutCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<AboutDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ApiResponse<AboutDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<AboutDto>> UpdateAsync(int id, AboutUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> ReorderAsync(List<AboutReorderRequest> request, CancellationToken ct = default);
    }
}
