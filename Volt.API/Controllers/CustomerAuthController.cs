using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.Application.Dtos.CustomerAuth;
using Volt.Application.Interfaces;
using Volt.API.Services;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class CustomerAuthController : CustomBaseController
    {
        private readonly ICustomerAuthService _customerAuthService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly AuthCookieService _authCookieService;

        public CustomerAuthController(
            ICustomerAuthService customerAuthService,
            IRefreshTokenService refreshTokenService,
            AuthCookieService authCookieService)
        {
            _customerAuthService = customerAuthService;
            _refreshTokenService = refreshTokenService;
            _authCookieService = authCookieService;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] CustomerRegisterRequest request, CancellationToken ct)
            => await CreateCustomerAuthResultAsync(await _customerAuthService.RegisterAsync(request, ct), ct);

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] CustomerLoginRequest request, CancellationToken ct)
            => await CreateCustomerAuthResultAsync(await _customerAuthService.LoginAsync(request, ct), ct);

        [HttpPost("social/google")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Google([FromBody] SocialTokenLoginRequest request, CancellationToken ct)
            => await CreateCustomerAuthResultAsync(await _customerAuthService.LoginWithGoogleAsync(request, ct), ct);

        [HttpPost("social/apple")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Apple([FromBody] SocialTokenLoginRequest request, CancellationToken ct)
            => await CreateCustomerAuthResultAsync(await _customerAuthService.LoginWithAppleAsync(request, ct), ct);

        [HttpPost("passkeys/register/options")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> BeginPasskeyRegistration([FromBody] PasskeyRegisterOptionsRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.BeginPasskeyRegistrationAsync(request, GetOrigin(), ct));

        [HttpPost("passkeys/register/complete")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> CompletePasskeyRegistration([FromBody] PasskeyCompleteRequest request, CancellationToken ct)
            => await CreateCustomerAuthResultAsync(await _customerAuthService.CompletePasskeyRegistrationAsync(request, ct), ct);

        [HttpPost("passkeys/login/options")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> BeginPasskeyLogin([FromBody] PasskeyLoginOptionsRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.BeginPasskeyLoginAsync(request, GetOrigin(), ct));

        [HttpPost("passkeys/login/complete")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> CompletePasskeyLogin([FromBody] PasskeyCompleteRequest request, CancellationToken ct)
            => await CreateCustomerAuthResultAsync(await _customerAuthService.CompletePasskeyLoginAsync(request, ct), ct);

        [Authorize(Roles = "Customer")]
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
            => CreateActionResult(await _customerAuthService.GetProfileAsync(GetCustomerId(), ct));

        [Authorize(Roles = "Customer")]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] CustomerUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.UpdateProfileAsync(GetCustomerId(), request, ct));

        private int GetCustomerId()
            => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private string GetOrigin()
            => Request.Headers.TryGetValue("Origin", out var origin)
                ? origin.ToString()
                : $"{Request.Scheme}://{Request.Host}";

        private async Task<IActionResult> CreateCustomerAuthResultAsync(
            Volt.Application.Dtos.ApiResponse<CustomerAuthResponse> result,
            CancellationToken ct)
        {
            if (result.Success && result.Data is not null)
            {
                var refreshToken = await _refreshTokenService.IssueAsync(
                    Volt.Domain.Enums.Role.Customer,
                    result.Data.User.Id,
                    ct);
                _authCookieService.SetRefreshCookie(Response, refreshToken);
            }

            return CreateActionResult(result);
        }
    }
}
