using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.Application.Dtos.Verification;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/document-verifications")]
    [ApiController]
    public sealed class DocumentVerificationsController : CustomBaseController
    {
        private readonly IDocumentVerificationService _verification;
        private readonly IDocumentVerificationInquiryService _inquiries;
        private readonly IAdminAccessService _access;

        public DocumentVerificationsController(IDocumentVerificationService verification, IDocumentVerificationInquiryService inquiries, IAdminAccessService access)
        {
            _verification = verification;
            _inquiries = inquiries;
            _access = access;
        }

        [HttpGet("public/{token}")]
        [EnableRateLimiting("verification-read")]
        public async Task<IActionResult> GetPublic(string token, CancellationToken ct)
            => CreateActionResult(await _verification.GetPublicAsync(token, ct));

        [HttpPost("public/{token}/inquiries")]
        [EnableRateLimiting("public-write")]
        public async Task<IActionResult> CreatePublicInquiry(string token, [FromBody] PublicDocumentVerificationInquiryRequest request, CancellationToken ct)
            => CreateActionResult(await _inquiries.CreatePublicAsync(token, request, ct));

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetRecent([FromQuery] int take = 100, CancellationToken ct = default)
        {
            if (!await CanManageAsync(ct)) return Forbid();
            return CreateActionResult(await _verification.GetRecentAsync(take, ct));
        }

        [Authorize]
        [HttpGet("document/{documentLogId:int}")]
        public async Task<IActionResult> GetAdmin(int documentLogId, CancellationToken ct)
        {
            if (!await CanManageAsync(ct)) return Forbid();
            return CreateActionResult(await _verification.GetAdminAsync(documentLogId, ct));
        }

        [Authorize]
        [HttpPost("document/{documentLogId:int}/reissue")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> Reissue(int documentLogId, CancellationToken ct)
        {
            var id = GetAdminUserId();
            if (!id.HasValue || !await CanManageAsync(ct)) return Forbid();
            return CreateActionResult(await _verification.ReissueAsync(documentLogId, id.Value, ct));
        }

        [Authorize]
        [HttpPost("document/{documentLogId:int}/revoke")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> Revoke(int documentLogId, [FromBody] RevokeDocumentVerificationRequest request, CancellationToken ct)
        {
            var id = GetAdminUserId();
            if (!id.HasValue || !await CanManageAsync(ct)) return Forbid();
            return CreateActionResult(await _verification.RevokeAsync(documentLogId, request.Reason, id.Value, ct));
        }

        [Authorize]
        [HttpPut("document/{documentLogId:int}/project")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> LinkProject(int documentLogId, [FromBody] LinkVerificationProjectRequest request, CancellationToken ct)
        {
            var id = GetAdminUserId();
            if (!id.HasValue || !await CanManageAsync(ct)) return Forbid();
            return CreateActionResult(await _verification.LinkProjectAsync(documentLogId, request.AdminTrackedProjectId, id.Value, ct));
        }

        [Authorize]
        [HttpGet("project/{adminTrackedProjectId:int}")]
        public async Task<IActionResult> GetForProject(int adminTrackedProjectId, CancellationToken ct)
        {
            if (!await CanManageAsync(ct)) return Forbid();
            return CreateActionResult(await _verification.GetForProjectAsync(adminTrackedProjectId, ct));
        }

        private int? GetAdminUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        private async Task<bool> CanManageAsync(CancellationToken ct)
        {
            var id = GetAdminUserId();
            return id.HasValue && (await _access.GetSessionAsync(id.Value, ct))?.IsSuperAdmin == true;
        }
    }
}
