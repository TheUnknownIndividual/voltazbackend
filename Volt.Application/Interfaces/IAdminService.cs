using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Admin;

namespace Volt.Application.Interfaces
{
    public interface IAdminService
    {
        Task<ApiResponse<AdminDto>> CreateAsync(AdminCreateRequest request, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<AdminDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ApiResponse<AdminDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<AdminDto>> UpdateAsync(int id, AdminUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> ChangePasswordAsync(int id, AdminChangePasswordRequest request, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<AdminDto>> UpdateAccessAsync(int id, AdminUpdateAccessRequest request, int actorAdminUserId, CancellationToken ct = default);
        Task<ApiResponse<AdminUserActivityPageDto>> GetActivityAsync(int id, int page, int pageSize, CancellationToken ct = default);
    }
}
