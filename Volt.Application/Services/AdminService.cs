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

        public Task<ApiResponse<NoContentDto>> Create(string name, string password)
        {
            
            var repo = _uow.Repository<AdminUser>();

           
            _passwordHelper.CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);

            var newAdmin = new AdminUser
            {
                Username = name,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = Role.Admin,
                Is_active = true
            };

            repo.AddAsync(newAdmin);
            return _uow.SaveChangesAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occurred while creating the admin user.");
                }
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            });
        }
    }
}
