using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class SolarYieldController : CustomBaseController
    {
        private readonly ISolarYieldService _service;

        public SolarYieldController(ISolarYieldService service)
        {
            _service = service;
        }

        /// <summary>Annual kWh per kWp from PVGIS. Omit tilt for optimal angles; azimuth: 0 = south, -90 = east, 90 = west.</summary>
        [HttpGet]
        [EnableRateLimiting("public-analytics")]
        public async Task<IActionResult> Get([FromQuery] double lat, [FromQuery] double lon, [FromQuery] int? tilt, [FromQuery] int? azimuth, CancellationToken ct)
        {
            var result = await _service.GetYieldAsync(lat, lon, tilt, azimuth, ct);
            if (result.Success)
            {
                Response.Headers.CacheControl = "public, max-age=86400";
            }
            return CreateActionResult(result);
        }
    }
}
