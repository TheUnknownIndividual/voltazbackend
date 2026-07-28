using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.Application.Dtos.Admin;
using Volt.Application.Interfaces;
using Volt.Application.Services;
using Volt.API.Services;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminAuthController : CustomBaseController
    {
        private readonly IAdminAuthService _adminAuthService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly AuthCookieService _authCookieService;

        public AdminAuthController(
            IAdminAuthService adminAuthService,
            IRefreshTokenService refreshTokenService,
            AuthCookieService authCookieService)
        {
            _adminAuthService = adminAuthService;
            _refreshTokenService = refreshTokenService;
            _authCookieService = authCookieService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("admin-auth")]
        public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken ct)
        {
            var token = await _adminAuthService.LoginAsync(request, ct);
            if (token.Success && token.Data is not null)
            {
                var refreshToken = await _refreshTokenService.IssueAsync(
                    Volt.Domain.Enums.Role.Admin,
                    token.Data.UserId,
                    ct);
                _authCookieService.SetRefreshCookie(Response, refreshToken);
            }

            return CreateActionResult(token);
        }
    }
}
