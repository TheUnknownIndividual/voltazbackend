using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Volt.Application.Dtos.Order;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class OrdersController : CustomBaseController
    {
        private readonly IOrderService _service;

        public OrdersController(IOrderService service)
        {
            _service = service;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] byte? status, CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(status, ct));

        [Authorize(Roles = "Customer")]
        [HttpGet("my")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
            => CreateActionResult(await _service.GetByCustomerEmailAsync(User.FindFirstValue(ClaimTypes.Email), ct));

        [Authorize(Roles = "Admin")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, ct));

        [HttpGet("lookup")]
        public async Task<IActionResult> Lookup([FromQuery] string orderNumber, [FromQuery] string email, CancellationToken ct)
            => CreateActionResult(await _service.LookupAsync(orderNumber, email, ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OrderCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, ct));

        [Authorize(Roles = "Admin")]
        [HttpPatch("status")]
        public async Task<IActionResult> UpdateStatus([FromQuery] int id, [FromBody] OrderStatusUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateStatusAsync(id, request, ct));

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id:int}/viewed")]
        public async Task<IActionResult> MarkViewed(int id, CancellationToken ct)
            => CreateActionResult(await _service.MarkViewedAsync(id, ct));
    }
}
