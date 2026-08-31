using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.API.Infrastructure.Localization;
using Volt.Application.Dtos.Blog;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class BlogsController : CustomBaseController
    {
        private readonly IBlogService _service;
        private readonly IAcceptLanguageService _acceptLanguageService;

        public BlogsController(IBlogService service, IAcceptLanguageService acceptLanguageService)
        {
            _service = service;
            _acceptLanguageService = acceptLanguageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(_acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpGet("admin")]
        public async Task<IActionResult> GetAllAdmin(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAdminAsync(null, ct));

        [Authorize]
        [HttpGet("admin/{id:int}")]
        public async Task<IActionResult> GetByIdAdmin(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAdminAsync(id, null, ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BlogCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] BlogUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpPatch("admin/{id:int}/status")]
        public async Task<IActionResult> SetStatus(int id, [FromBody] BlogStatusUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.SetStatusAsync(id, request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));
    }
}
