using Volt.Application.Dtos.Admin;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IAdminAccessService
    {
        Task<AdminSessionDto?> GetSessionAsync(int adminUserId, CancellationToken ct = default);
        Task<bool> HasPageAsync(int adminUserId, AdminPage page, CancellationToken ct = default);
        Task<bool> CanDeleteProjectsAsync(int adminUserId, CancellationToken ct = default);
        Task<bool> CanEditProjectsAsync(int adminUserId, CancellationToken ct = default);
    }
}
