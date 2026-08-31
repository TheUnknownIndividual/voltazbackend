using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volt.API.Infrastructure.Localization;
using Volt.Application.Dtos.ProdcutCategory;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductCategoriesController : CustomBaseController
    {
        private readonly IProductCategoryService _service;
        private readonly IAcceptLanguageService _acceptLanguageService;

        public ProductCategoriesController(IProductCategoryService service, IAcceptLanguageService acceptLanguageService)
        {
            _service = service;
            _acceptLanguageService = acceptLanguageService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductCategoryCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeAllLanguages, CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(
                includeAllLanguages
                    ? null
                    : _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()),
                ct));

        [HttpGet("homepage")]
        public async Task<IActionResult> GetHomePage(CancellationToken ct)
            => CreateActionResult(await _service.GetHomePageAsync(_acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [HttpGet("seo/{seoKey}")]
        public async Task<IActionResult> GetBySeoKey(string seoKey, CancellationToken ct)
            => CreateActionResult(await _service.GetBySeoKeyAsync(
                seoKey,
                _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()),
                ct));

        [Authorize]
        [HttpGet("{id:int}/product-options")]
        public async Task<IActionResult> GetProductOptions(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetProductOptionsAsync(id, ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, ct));

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductCategoryUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()), ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));
    }
}
