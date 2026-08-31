using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.Application.Dtos.HomeSlider;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class HomeSlidersController : CustomBaseController
{
    private readonly IHomeSliderService _service;

    public HomeSlidersController(IHomeSliderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => CreateActionResult(await _service.GetAsync(ct));

    [Authorize]
    [HttpPut]
    [EnableRateLimiting("protected-write")]
    public async Task<IActionResult> Update([FromBody] HomeSliderUpdateRequest request, CancellationToken ct)
        => CreateActionResult(await _service.UpdateAsync(request, ct));
}
