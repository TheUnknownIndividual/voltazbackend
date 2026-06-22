using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.Product;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class ProductsController : CustomBaseController
    {
        private readonly IProductService _service;

        public ProductsController(IProductService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, ct));

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] ProductFiltrDto dto, CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(dto, ct));

        [HttpGet("HomePage")]
        public async Task<IActionResult> GetAllForHomePage([FromQuery] ProductFiltrHomePageDto dto ,CancellationToken ct)
            => CreateActionResult(await _service.GetAllForHomePageAsync(dto, ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, ct));

        [Authorize]
        [HttpPut("ShowHomePage")]
        public async Task<IActionResult> ShowHomePage(ProductShowHomePageDto dto, CancellationToken ct)
            => CreateActionResult(await _service.ShowHomePage(dto, ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));

        [Authorize]
        [HttpGet("ShowHomePageProductCount")]
        public async Task<IActionResult> ShowHomePageProductCount(CancellationToken ct)
            => CreateActionResult(await _service.ShowHomePageProductCount(ct));
    }
}
