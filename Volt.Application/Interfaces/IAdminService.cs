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
        Task<ApiResponse<NoContentDto>> Create(string name, string password);
        Task<ApiResponse<IReadOnlyList<AdminDto>>> GetAllAsync(CancellationToken ct = default);
        Task<ApiResponse<AdminDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<AdminDto>> UpdateAsync(int id, AdminUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> ChangePasswordAsync(int id, AdminChangePasswordRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
    }
}
