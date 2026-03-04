using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.Admin;
using Volt.Application.Interfaces;
using Volt.Application.Services;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminAuthController : CustomBaseController
    {
        private readonly IAdminAuthService _adminAuthService;

        public AdminAuthController(IAdminAuthService adminAuthService)
        {
            _adminAuthService = adminAuthService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken ct)
        {
            var token = await _adminAuthService.LoginAsync(request, ct);
            return CreateActionResult(token);
        }
    }
}
