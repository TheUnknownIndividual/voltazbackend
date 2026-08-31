using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.ServiceRequest;
using Volt.Application.Interfaces;
using Volt.Domain.Enums;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class ServiceRequestsController : CustomBaseController
    {
        private readonly IServiceRequestService _service;

        public ServiceRequestsController(IServiceRequestService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] byte? status, CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(status, ct));

        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServiceRequestCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ServiceRequestUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, ct));

        [Authorize]
        [HttpPatch("status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] ServiceRequestStatusUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateStatusAsync(id, request, ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));

        [Authorize]
        [HttpPatch("{id:int}/viewed")]
        public async Task<IActionResult> MarkViewed(int id, CancellationToken ct)
            => CreateActionResult(await _service.MarkViewedAsync(id, ct));
    }
}
