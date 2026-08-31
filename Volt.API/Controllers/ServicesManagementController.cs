using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volt.API.Infrastructure.Localization;
using Volt.Application.Dtos.ServiceManagement;
using Volt.Application.Interfaces;
using Volt.Domain.Enums;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServicesManagementController : CustomBaseController
    {
        private readonly IServiceManagementService _service;
        private readonly IAcceptLanguageService _acceptLanguageService;

        public ServicesManagementController(IServiceManagementService service, IAcceptLanguageService acceptLanguageService)
        {
            _service = service;
            _acceptLanguageService = acceptLanguageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(_acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpGet("admin")]
        public async Task<IActionResult> GetAllForAdmin(CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(null, ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [HttpGet("page/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
            => CreateActionResult(await _service.GetBySlugAsync(slug, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [HttpGet("category-settings")]
        public async Task<IActionResult> GetCategorySettings(CancellationToken ct)
            => CreateActionResult(await _service.GetCategorySettingsAsync(ct));

        [Authorize]
        [HttpPut("category-settings/{category:int}")]
        public async Task<IActionResult> UpdateCategorySetting(
            ServiceCategory category,
            [FromBody] ServiceCategorySettingUpdateRequest request,
            CancellationToken ct)
            => CreateActionResult(await _service.UpdateCategorySettingAsync(category, request, ct));

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServiceManagementCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ServiceManagementUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));

    }
}
