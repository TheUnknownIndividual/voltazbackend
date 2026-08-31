using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Admin;
using Volt.Application.Interfaces;
using Volt.Application.Security;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class AdminService : IAdminService
    {
        private readonly IUnitOfWork _uow;
        private readonly PasswordHelper _passwordHelper;
        private readonly IAdminAuditService _audit;

        public AdminService(IUnitOfWork uow, PasswordHelper passwordHelper, IAdminAuditService audit)
        {
            _uow = uow;
            _passwordHelper = passwordHelper;
            _audit = audit;
        }
        
        public async Task<ApiResponse<IReadOnlyList<AdminDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var list = (await _uow.Repository<AdminUser>().ListNoTrackingAsync(x => x.IsActive, ct)).OrderBy(x => x.Id).ToList();
            var permissions = await _uow.Repository<AdminPagePermission>().ListNoTrackingAsync(ct);
            var newlist = list.Select(admin => ToDto(admin, permissions.Where(x => x.AdminUserId == admin.Id).Select(x => x.Page)))
                .ToList();

            return ApiResponse<IReadOnlyList<AdminDto>>.SuccessResponse(newlist);
        }

        public async Task<ApiResponse<AdminDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var admin = await _uow.Repository<AdminUser>().FirstOrDefaultNoTrackingAsync(x => x.Id == id, ct);

            if (admin is null)
            {
                return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.ADMIN_NOT_FOUND, "Admin not found");
            }
            var pages = await _uow.Repository<AdminPagePermission>().ListNoTrackingAsync(x => x.AdminUserId == id, ct);
            return ApiResponse<AdminDto>.SuccessResponse(ToDto(admin, pages.Select(x => x.Page)));
        }

        public async Task<ApiResponse<AdminDto>> UpdateAsync(int id, AdminUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var admin = await repo.FirstOrDefaultAsync(x=> x.Id == id, ct);

            if (admin is null)
            {
                return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.ADMIN_NOT_FOUND, "Admin not found");
            }

            admin.Username = request.Username;

            repo.Update(admin);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<AdminDto>.SuccessResponse(ToDto(admin, Array.Empty<AdminPage>()));
        }

        public async Task<ApiResponse<NoContentDto>> ChangePasswordAsync(int id, AdminChangePasswordRequest request, int actorAdminUserId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var admin = await repo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (admin is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.ADMIN_NOT_FOUND, "Admin not found");
            }
            if (admin.IsEffectiveSuperAdmin)
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Primary admin password cannot be changed here.");

            _passwordHelper.CreatePasswordHash(request.NewPassword, out byte[] passwordHash, out byte[] passwordSalt);

            admin.PasswordHash = passwordHash;
            admin.PasswordSalt = passwordSalt;

            repo.Update(admin);
            await _uow.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorAdminUserId, null, "SUBADMIN_PASSWORD_RESET", "AdminUser", admin.Id.ToString(), admin.Username, true, ct);

            return ApiResponse<NoContentDto>.SuccessResponse(null);
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, int actorAdminUserId, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var admin = await repo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (admin is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.ADMIN_NOT_FOUND, "Admin not found");
            }
            if (admin.IsEffectiveSuperAdmin)
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "A full admin cannot be deleted.");

            // Admin users can be referenced by audits, tasks and execution records.
            // Deactivate instead of deleting the row so those records remain valid.
            admin.IsActive = false;
            admin.IsStakeholder = false;
            admin.TelegramChatId = null;
            repo.Update(admin);
            var permissionRepository = _uow.Repository<AdminPagePermission>();
            foreach (var permission in await permissionRepository.ListNoTrackingAsync(x => x.AdminUserId == id, ct))
                permissionRepository.Remove(permission);
            await _uow.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorAdminUserId, null, "SUBADMIN_DELETED", "AdminUser", id.ToString(), admin.Username, true, ct);

            return ApiResponse<NoContentDto>.SuccessResponse(null);
        }

        public async Task<ApiResponse<AdminDto>> CreateAsync(AdminCreateRequest request, int actorAdminUserId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Username and password are required.");
            var username = request.Username.Trim().ToLowerInvariant();
            if (await _uow.Repository<AdminUser>().AnyAsync(x => x.Username == username, ct))
                return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Username is already in use.");

            _passwordHelper.CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);
            var admin = new AdminUser
            {
                Username = username,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = Role.Admin,
                IsActive = true,
                IsSuperAdmin = false,
                CanDeleteProjects = request.CanDeleteProjects,
                CanEditProjects = request.CanEditProjects,
                CanApproveWarehouseMovements = request.CanApproveWarehouseMovements,
                IsStakeholder = request.IsStakeholder,
                MonthlySalary = request.MonthlySalary is > 0 ? request.MonthlySalary : null,
                TelegramChatId = request.TelegramChatId
            };
            await _uow.Repository<AdminUser>().AddAsync(admin, ct);
            await _uow.SaveChangesAsync(ct);
            foreach (var page in request.AllowedPages?.Distinct() ?? Array.Empty<Volt.Domain.Enums.AdminPage>())
                await _uow.Repository<AdminPagePermission>().AddAsync(new AdminPagePermission { AdminUserId = admin.Id, Page = page }, ct);
            await _uow.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorAdminUserId, null, "SUBADMIN_CREATED", "AdminUser", admin.Id.ToString(), admin.Username, true, ct);
            return ApiResponse<AdminDto>.SuccessResponse(ToDto(admin, request.AllowedPages ?? Array.Empty<AdminPage>()));
        }

        public async Task<ApiResponse<AdminDto>> UpdateAccessAsync(int id, AdminUpdateAccessRequest request, int actorAdminUserId, CancellationToken ct = default)
        {
            var admin = await _uow.Repository<AdminUser>().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (admin is null) return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.ADMIN_NOT_FOUND, "Admin not found");
            if (admin.IsEffectiveSuperAdmin) return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Primary admin access cannot be restricted.");
            admin.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? admin.Username : request.DisplayName.Trim();
            admin.IsActive = request.IsActive;
            admin.CanDeleteProjects = request.CanDeleteProjects;
            admin.CanEditProjects = request.CanEditProjects;
            admin.CanApproveWarehouseMovements = request.CanApproveWarehouseMovements;
            admin.IsStakeholder = request.IsStakeholder;
            if (request.ClearSalary)
                admin.MonthlySalary = null;
            else if (request.MonthlySalary is > 0)
                admin.MonthlySalary = request.MonthlySalary;
            admin.TelegramChatId = request.TelegramChatId;
            var permissionRepository = _uow.Repository<AdminPagePermission>();
            var existingPermissions = await permissionRepository.ListNoTrackingAsync(x => x.AdminUserId == id, ct);
            foreach (var existing in existingPermissions) permissionRepository.Remove(existing);
            foreach (var page in (request.AllowedPages ?? Array.Empty<Volt.Domain.Enums.AdminPage>()).Distinct())
                await permissionRepository.AddAsync(new AdminPagePermission { AdminUserId = admin.Id, Page = page }, ct);
            _uow.Repository<AdminUser>().Update(admin);
            await _uow.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorAdminUserId, null, "SUBADMIN_ACCESS_UPDATED", "AdminUser", admin.Id.ToString(), admin.Username, true, ct);
            return ApiResponse<AdminDto>.SuccessResponse(ToDto(admin, request.AllowedPages ?? Array.Empty<AdminPage>()));
        }

        public async Task<ApiResponse<AdminUserActivityPageDto>> GetActivityAsync(int id, int page, int pageSize, CancellationToken ct = default)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
            var auditRepository = _uow.Repository<AdminAuditLog>();
            var total = await auditRepository.CountAsync(x => x.AdminUserId == id, ct);
            var logs = await auditRepository.ListNoTrackingAsync(x => x.AdminUserId == id, ct);
            var items = logs.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AdminUserActivityDto(x.Id, x.Action, x.TargetType, x.TargetId, x.Summary, x.Succeeded, x.CreatedAt)).ToList();
            return ApiResponse<AdminUserActivityPageDto>.SuccessResponse(new AdminUserActivityPageDto(items, total, page, pageSize));
        }

        private static AdminDto ToDto(AdminUser admin, IEnumerable<AdminPage> pages)
        {
            var isSuperAdmin = admin.IsEffectiveSuperAdmin;
            return new(
                admin.Id, admin.Username, string.IsNullOrWhiteSpace(admin.DisplayName) ? admin.Username : admin.DisplayName,
                admin.Role, admin.IsActive, isSuperAdmin, isSuperAdmin || admin.CanDeleteProjects, isSuperAdmin || admin.CanEditProjects,
                isSuperAdmin || admin.CanApproveWarehouseMovements, admin.IsStakeholder, admin.MonthlySalary.HasValue, admin.TelegramChatId,
                isSuperAdmin ? Enum.GetValues<Volt.Domain.Enums.AdminPage>().ToList() : pages.Distinct().OrderBy(x => x).ToList());
        }
    }
}
