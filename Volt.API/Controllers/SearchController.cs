using Microsoft.AspNetCore.Mvc;
using Volt.API.Infrastructure.Localization;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class SearchController : CustomBaseController
    {
        private readonly ISearchService _service;
        private readonly IAcceptLanguageService _acceptLanguageService;

        public SearchController(ISearchService service, IAcceptLanguageService acceptLanguageService)
        {
            _service = service;
            _acceptLanguageService = acceptLanguageService;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int productLimit = 6, CancellationToken ct = default)
            => CreateActionResult(await _service.SearchAsync(
                query,
                productLimit,
                _acceptLanguageService.Resolve(Request.Headers.AcceptLanguage.ToString()),
                ct));
    }
}
