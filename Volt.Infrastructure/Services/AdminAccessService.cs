using Microsoft.EntityFrameworkCore;
using Volt.Application.Dtos.Admin;
using Volt.Application.Interfaces;
using Volt.Domain.Enums;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services
{
    public sealed class AdminAccessService : IAdminAccessService
    {
        private readonly DataContext _context;
        public AdminAccessService(DataContext context) => _context = context;

        public async Task<AdminSessionDto?> GetSessionAsync(int adminUserId, CancellationToken ct = default)
        {
            var admin = await _context.AdminUsers.AsNoTracking()
                .Include(x => x.PagePermissions)
                .FirstOrDefaultAsync(x => x.Id == adminUserId && x.IsActive, ct);
            if (admin is null) return null;

            var isSuperAdmin = admin.IsEffectiveSuperAdmin;

            var pages = isSuperAdmin
                ? Enum.GetValues<AdminPage>().ToList()
                : admin.PagePermissions.Select(x => x.Page).Distinct().OrderBy(x => x).ToList();

            return new AdminSessionDto(
                admin.Id,
                admin.Username,
                string.IsNullOrWhiteSpace(admin.DisplayName) ? admin.Username : admin.DisplayName,
                isSuperAdmin,
                isSuperAdmin || admin.CanDeleteProjects,
                isSuperAdmin || admin.CanEditProjects,
                isSuperAdmin || admin.CanApproveWarehouseMovements,
                isSuperAdmin || pages.Contains(AdminPage.Accounting),
                pages);
        }

        public async Task<bool> HasPageAsync(int adminUserId, AdminPage page, CancellationToken ct = default)
        {
            var session = await GetSessionAsync(adminUserId, ct);
            return session is not null && (session.IsSuperAdmin || session.AllowedPages.Contains(page));
        }

        public async Task<bool> CanDeleteProjectsAsync(int adminUserId, CancellationToken ct = default)
        {
            var session = await GetSessionAsync(adminUserId, ct);
            return session is not null && session.CanDeleteProjects;
        }

        public async Task<bool> CanEditProjectsAsync(int adminUserId, CancellationToken ct = default)
        {
            var session = await GetSessionAsync(adminUserId, ct);
            return session is not null && session.CanEditProjects;
        }
    }
}
