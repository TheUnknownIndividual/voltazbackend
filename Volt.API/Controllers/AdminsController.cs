using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.Admin;
using Volt.Application.Interfaces;
using System.Security.Claims;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminsController : CustomBaseController
    {
        private readonly IAdminService _service;
        private readonly IAdminAccessService _access;

        public AdminsController(IAdminService service, IAdminAccessService access)
        {
            _service = service;
            _access = access;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var id = GetAdminUserId();
            if (!id.HasValue || !await CanViewUsersAsync(id.Value, ct)) return Forbid();
            return CreateActionResult(await _service.GetAllAsync(ct));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            var id = GetAdminUserId();
            if (!id.HasValue) return Forbid();
            var session = await _access.GetSessionAsync(id.Value, ct);
            return session is null ? Forbid() : Ok(new { success = true, data = session });
        }

        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            if (!await IsSuperAdminAsync(ct)) return Forbid();
            return CreateActionResult(await _service.GetByIdAsync(id, ct));
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AdminUpdateRequest request, CancellationToken ct)
        {
            if (!await IsSuperAdminAsync(ct)) return Forbid();
            return CreateActionResult(await _service.UpdateAsync(id, request, ct));
        }

        [Authorize]
        [HttpPut("{id:int}/access")]
        public async Task<IActionResult> UpdateAccess(int id, [FromBody] AdminUpdateAccessRequest request, CancellationToken ct)
        {
            var actorId = GetAdminUserId();
            if (!actorId.HasValue || !await IsSuperAdminAsync(ct)) return Forbid();
            return CreateActionResult(await _service.UpdateAccessAsync(id, request, actorId.Value, ct));
        }

        [Authorize]
        [HttpPut("{id:int}/password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] AdminChangePasswordRequest request, CancellationToken ct)
        {
            var actorId = GetAdminUserId();
            if (!actorId.HasValue || !await IsSuperAdminAsync(ct)) return Forbid();
            var result =  await _service.ChangePasswordAsync(id, request, actorId.Value, ct);
            return CreateActionResult(result);
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var actorId = GetAdminUserId();
            if (!actorId.HasValue || !await IsSuperAdminAsync(ct)) return Forbid();
            var result = await _service.DeleteAsync(id, actorId.Value, ct);
            return CreateActionResult(result);
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] AdminCreateRequest request, CancellationToken ct)
        {
            var actorId = GetAdminUserId();
            if (!actorId.HasValue || !await IsSuperAdminAsync(ct)) return Forbid();
            return CreateActionResult(await _service.CreateAsync(request, actorId.Value, ct));
        }

        [Authorize]
        [HttpGet("{id:int}/activity")]
        public async Task<IActionResult> Activity(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        {
            if (!await IsSuperAdminAsync(ct)) return Forbid();
            return CreateActionResult(await _service.GetActivityAsync(id, page, pageSize, ct));
        }

        private int? GetAdminUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out var adminId) ? adminId : null;
        }

        private async Task<bool> IsSuperAdminAsync(CancellationToken ct)
        {
            var id = GetAdminUserId();
            if (!id.HasValue) return false;
            return (await _access.GetSessionAsync(id.Value, ct))?.IsSuperAdmin == true;
        }

        private async Task<bool> CanViewUsersAsync(int adminUserId, CancellationToken ct)
        {
            var session = await _access.GetSessionAsync(adminUserId, ct);
            return session?.IsSuperAdmin == true || session?.AllowedPages.Contains(Volt.Domain.Enums.AdminPage.Users) == true;
        }
    }
}
