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

        public AdminAuthService(IUnitOfWork uow, ITokenService tokenService, PasswordHelper passwordHelper)
        {
            _uow = uow;
            _tokenService = tokenService;
            _passwordHelper = passwordHelper;
        }

        public async Task<ApiResponse<TokenDto>> LoginAsync(AdminLoginRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<AdminUser>(); 

            var admin = await repo.FirstOrDefaultNoTrackingAsync(u => u.Username.ToLower() == request.Username.ToLower(), ct);

            if (admin is null)
                return ApiResponse<TokenDto>.ErrorResponse(ErrorCode.INVALID_USERNAME, null);

            if (!_passwordHelper.VerifyPassword(request.Password, admin.PasswordHash, admin.PasswordSalt))
                return ApiResponse<TokenDto>.ErrorResponse(ErrorCode.INVALID_PASSWORD, null);

            var token = _tokenService.CreateAdminAccessToken(admin);

            return ApiResponse<TokenDto>.SuccessResponse(token);
        }
    }
}
