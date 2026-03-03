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

        public AdminService(IUnitOfWork uow, PasswordHelper passwordHelper)
        {
            _uow = uow;
            _passwordHelper = passwordHelper;
        }
        
        public async Task<ApiResponse<IReadOnlyList<AdminDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var list = await repo.ListNoTrackingAsync(ct);

          var newlist =  list.OrderBy(x => x.Id)
                .Select(x => new AdminDto(
                x.Id,
                x.Username, 
                x.Role,
                x.Is_active))
                .ToList();

            return ApiResponse<IReadOnlyList<AdminDto>>.SuccessResponse(newlist);
        }

        public async Task<ApiResponse<AdminDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var admin =  await repo.FirstOrDefaultNoTrackingAsync(x => x.Id == id, ct);

            var data = new AdminDto (

                admin.Id,
                admin.Username,
                admin.Role,
                admin.Is_active
                
                );

            return ApiResponse<AdminDto>.SuccessResponse(data);
        }

        public async Task<ApiResponse<AdminDto>> CreateAsync(AdminCreateRequest request, CancellationToken ct = default)
        {
           var repo = _uow.Repository<AdminUser>();

            var exists = await repo.AnyAsync(x => x.Username == request.Username, ct);

            if (exists)
            {
                return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.INVALID_PASSWORD,"Username already exists.");
            }

            _passwordHelper.CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var admin = new AdminUser
            {
                Username = request.Username,
                Role = Role.Admin,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Is_active = true
            };

            await repo.AddAsync(admin, ct);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<AdminDto>.SuccessResponse(null);
        }

        public async Task<ApiResponse<AdminDto>> UpdateAsync(int id, AdminUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var admin = await repo.FirstOrDefaultAsync(x=> x.Id == id, ct);

            if (admin == null)
            {
                return ApiResponse<AdminDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "Admin user not found.");
            }

            admin.Username = request.Username;

            repo.Update(admin);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<AdminDto>.SuccessResponse(null);
        }

        public async Task<ApiResponse<NoContentDto>> ChangePasswordAsync(int id, AdminChangePasswordRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var admin = await repo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (admin == null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "Admin user not found.");
            }

            _passwordHelper.CreatePasswordHash(request.NewPassword, out byte[] passwordHash, out byte[] passwordSalt);

            admin.PasswordHash = passwordHash;
            admin.PasswordSalt = passwordSalt;

            repo.Update(admin);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<NoContentDto>.SuccessResponse(null);
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>();
            var admin = await repo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (admin == null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "Admin user not found.");
            }

            repo.Remove(admin);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<NoContentDto>.SuccessResponse(null);
        }
    }
}
