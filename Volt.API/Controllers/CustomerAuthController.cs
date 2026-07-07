using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.CustomerAuth;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class CustomerAuthController : CustomBaseController
    {
        private readonly ICustomerAuthService _customerAuthService;

        public CustomerAuthController(ICustomerAuthService customerAuthService)
        {
            _customerAuthService = customerAuthService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CustomerRegisterRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.RegisterAsync(request, ct));

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] CustomerLoginRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.LoginAsync(request, ct));

        [HttpPost("social/google")]
        public async Task<IActionResult> Google([FromBody] SocialTokenLoginRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.LoginWithGoogleAsync(request, ct));

        [HttpPost("social/apple")]
        public async Task<IActionResult> Apple([FromBody] SocialTokenLoginRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.LoginWithAppleAsync(request, ct));

        [HttpPost("passkeys/register/options")]
        public async Task<IActionResult> BeginPasskeyRegistration([FromBody] PasskeyRegisterOptionsRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.BeginPasskeyRegistrationAsync(request, GetOrigin(), ct));

        [HttpPost("passkeys/register/complete")]
        public async Task<IActionResult> CompletePasskeyRegistration([FromBody] PasskeyCompleteRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.CompletePasskeyRegistrationAsync(request, ct));

        [HttpPost("passkeys/login/options")]
        public async Task<IActionResult> BeginPasskeyLogin([FromBody] PasskeyLoginOptionsRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.BeginPasskeyLoginAsync(request, GetOrigin(), ct));

        [HttpPost("passkeys/login/complete")]
        public async Task<IActionResult> CompletePasskeyLogin([FromBody] PasskeyCompleteRequest request, CancellationToken ct)
            => CreateActionResult(await _customerAuthService.CompletePasskeyLoginAsync(request, ct));

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
    }
}
