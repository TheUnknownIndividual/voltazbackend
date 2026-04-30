using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.API.Infrastructure.Localization;
using Volt.Application.Dtos.NewsPost;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class NewsPostsController : CustomBaseController
    {
        private readonly INewsPostService _service;
        private readonly IAcceptLanguageService _acceptLanguageService;

        public NewsPostsController(INewsPostService service, IAcceptLanguageService acceptLanguageService)
        {
            _service = service;
            _acceptLanguageService = acceptLanguageService;
        }
        [HttpGet("GetAllForPublic")]
        public async Task<IActionResult> GetAllForPublic(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsyncPublic(_acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(_acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NewsPostCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] NewsPostUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));
    }
}
