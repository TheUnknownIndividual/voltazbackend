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
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class AdminAuthService : IAdminAuthService
    {
        private readonly IUnitOfWork _uow;
        private readonly ITokenService _tokenService;
        private readonly PasswordHelper _passwordHelper;
        private readonly IAdminAuditService _audit;

        public AdminAuthService(IUnitOfWork uow, ITokenService tokenService, PasswordHelper passwordHelper, IAdminAuditService audit)
        {
            _uow = uow;
            _tokenService = tokenService;
            _passwordHelper = passwordHelper;
            _audit = audit;
        }

        public async Task<ApiResponse<TokenDto>> LoginAsync(AdminLoginRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>(); 

            var admin = await repo.FirstOrDefaultNoTrackingAsync(u => u.Username.ToLower() == request.Username.ToLower(), ct);

            if (admin is null)
            {
                await _audit.WriteAsync(null, request.Username, "ADMIN_LOGIN", "AdminUser", null, "Unknown username", false, ct);
                return ApiResponse<TokenDto>.ErrorResponse(ErrorCode.INVALID_USERNAME, null);
            }

            if (!admin.IsActive)
            {
                await _audit.WriteAsync(admin.Id, admin.Username, "ADMIN_LOGIN", "AdminUser", admin.Id.ToString(), "Account inactive", false, ct);
                return ApiResponse<TokenDto>.ErrorResponse(ErrorCode.INVALID_USERNAME, null);
            }

            if (!_passwordHelper.VerifyPassword(request.Password, admin.PasswordHash, admin.PasswordSalt))
            {
                await _audit.WriteAsync(admin.Id, admin.Username, "ADMIN_LOGIN", "AdminUser", admin.Id.ToString(), "Invalid password", false, ct);
                return ApiResponse<TokenDto>.ErrorResponse(ErrorCode.INVALID_PASSWORD, null);
            }

            var token = _tokenService.CreateAdminAccessToken(admin) with { UserId = admin.Id };
            await _audit.WriteAsync(admin.Id, admin.Username, "ADMIN_LOGIN", "AdminUser", admin.Id.ToString(), "Signed in", true, ct);

            return ApiResponse<TokenDto>.SuccessResponse(token);
        }
    }
}
