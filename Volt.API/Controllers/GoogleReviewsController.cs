using Microsoft.AspNetCore.Mvc;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class GoogleReviewsController : CustomBaseController
    {
        private readonly IGoogleReviewsService _service;

        public GoogleReviewsController(IGoogleReviewsService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
            => CreateActionResult(await _service.GetReviewsAsync(ct));
    }
}
