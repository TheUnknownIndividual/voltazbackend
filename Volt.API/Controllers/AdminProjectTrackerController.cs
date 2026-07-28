using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Volt.Application.Dtos.AdminProjectTracker;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public sealed class AdminProjectTrackerController : CustomBaseController
    {
        private readonly IAdminProjectTrackerService _service;

        public AdminProjectTrackerController(IAdminProjectTrackerService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AdminTrackedProjectUpsertRequest request, CancellationToken ct)
        {
            var actorId = AdminId();
            return actorId.HasValue ? CreateActionResult(await _service.CreateAsync(request, actorId.Value, ct)) : Forbid();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AdminTrackedProjectUpsertRequest request, CancellationToken ct)
        {
            var actorId = AdminId();
            return actorId.HasValue ? CreateActionResult(await _service.UpdateAsync(id, request, actorId.Value, ct)) : Forbid();
        }

        [HttpPost("{id:int}/stakeholder-review-request")]
        public async Task<IActionResult> RequestStakeholderReview(int id, CancellationToken ct)
        {
            var actorId = AdminId();
            return actorId.HasValue ? CreateActionResult(await _service.RequestStakeholderReviewAsync(id, actorId.Value, ct)) : Forbid();
        }

        [HttpPost("{id:int}/stakeholder-approval-retry")]
        public async Task<IActionResult> RetryStakeholderApproval(int id, CancellationToken ct)
        {
            var actorId = AdminId();
            return actorId.HasValue ? CreateActionResult(await _service.RetryStakeholderApprovalAsync(id, actorId.Value, ct)) : Forbid();
        }

        [HttpPost("{id:int}/attachments")]
        public async Task<IActionResult> AddAttachment(int id, [FromBody] AdminTrackedProjectAttachmentRequest request, CancellationToken ct)
            => CreateActionResult(await _service.AddAttachmentAsync(id, request, ct));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));

        private int? AdminId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}
