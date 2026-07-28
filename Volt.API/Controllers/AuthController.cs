using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos;
using Volt.Application.Dtos.AuthRefresh;
using Volt.Application.Interfaces;
using Volt.API.Services;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class AuthController : CustomBaseController
    {
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly AuthCookieService _authCookieService;

        public AuthController(
            IRefreshTokenService refreshTokenService,
            AuthCookieService authCookieService)
        {
            _refreshTokenService = refreshTokenService;
            _authCookieService = authCookieService;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            var currentRefreshToken = Request.Cookies[AuthCookieService.RefreshCookieName];
            var session = await _refreshTokenService.RefreshAsync(currentRefreshToken, ct);
            if (session is null)
            {
                _authCookieService.ClearRefreshCookie(Response);
                return Unauthorized(ApiResponse<object>.ErrorResponse("AUTH_REFRESH_FAILED", "Authentication refresh is required."));
            }

            _authCookieService.SetRefreshCookie(Response, session.RefreshToken);
            return CreateActionResult(ApiResponse<RefreshResponseDto>.SuccessResponse(new RefreshResponseDto
            {
                AccessToken = session.AccessToken,
                User = session.User,
                RefreshToken = session.RefreshToken,
            }));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            await _refreshTokenService.RevokeAsync(Request.Cookies[AuthCookieService.RefreshCookieName], ct);
            _authCookieService.ClearRefreshCookie(Response);
            return Ok(ApiResponse<object>.SuccessResponse(null));
        }
    }
}
