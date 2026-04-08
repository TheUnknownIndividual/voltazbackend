using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.ApplicationType;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class ApplicationTypesController : CustomBaseController
    {
        private readonly IApplicationTypeService _service;

        public ApplicationTypesController(IApplicationTypeService service)
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
        public async Task<IActionResult> Create([FromBody] ApplicationTypeCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ApplicationTypeUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));
    }
}

