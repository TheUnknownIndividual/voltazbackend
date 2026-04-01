using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.ServiceManagement;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServicesManagementController : CustomBaseController
    {
        private readonly IServiceManagementService _service;

        public ServicesManagementController(IServiceManagementService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, ct));

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServiceManagementCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ServiceManagementUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));

        [Authorize]
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder([FromBody] List<ServiceManagementReorderRequest> request, CancellationToken ct)
            => CreateActionResult(await _service.ReorderAsync(request, ct));
    }
}
