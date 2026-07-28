using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.Application.Dtos.Verification;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/document-verification-inquiries")]
    [ApiController]
    public sealed class DocumentVerificationInquiriesController : CustomBaseController
    {
        private readonly IDocumentVerificationInquiryService _service;
        private readonly IAdminAccessService _access;

        public DocumentVerificationInquiriesController(IDocumentVerificationInquiryService service, IAdminAccessService access)
        {
            _service = service;
            _access = access;
        }

        [HttpGet]
        public async Task<IActionResult> GetPage([FromQuery] string? type, [FromQuery] string? status, [FromQuery] int? assignedAdminUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        {
            var actor = GetAdminUserId();
            if (!actor.HasValue) return Forbid();
            return CreateActionResult(await _service.GetPageAsync(actor.Value, type, status, assignedAdminUserId, page, pageSize, ct));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDetail(int id, CancellationToken ct)
        {
            var actor = GetAdminUserId();
            if (!actor.HasValue) return Forbid();
            return CreateActionResult(await _service.GetDetailAsync(id, actor.Value, ct));
        }

        [HttpPut("{id:int}/assignment")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> Assign(int id, [FromBody] AssignVerificationInquiryRequest request, CancellationToken ct)
        {
            var actor = GetAdminUserId();
            if (!actor.HasValue || !await IsFullAdminAsync(actor.Value, ct)) return Forbid();
            return CreateActionResult(await _service.AssignAsync(id, request.AssignedAdminUserId, actor.Value, ct));
        }

        [HttpPut("{id:int}/status")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateVerificationInquiryStatusRequest request, CancellationToken ct)
        {
            var actor = GetAdminUserId();
            if (!actor.HasValue) return Forbid();
            return CreateActionResult(await _service.UpdateStatusAsync(id, request.Status, actor.Value, ct));
        }

        private int? GetAdminUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        private async Task<bool> IsFullAdminAsync(int id, CancellationToken ct) => (await _access.GetSessionAsync(id, ct))?.IsSuperAdmin == true;
    }
}
