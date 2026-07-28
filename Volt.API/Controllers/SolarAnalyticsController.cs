using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Volt.Application.Dtos.SolarAnalytics;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class SolarAnalyticsController : CustomBaseController
    {
        private readonly ISolarAnalyticsService _service;

        public SolarAnalyticsController(ISolarAnalyticsService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpGet("projects")]
        public async Task<IActionResult> SearchProjects([FromQuery] string? query, CancellationToken ct)
            => CreateActionResult(await _service.SearchProjectsAsync(query, ct));

        [Authorize]
        [HttpPost("admin/docx-export")]
        public async Task<IActionResult> IssueDocxExport([FromBody] AdminSolarDocxExportRequest request, CancellationToken ct)
            => CreateActionResult(await _service.IssueDocxExportAsync(request, GetAdminUserId(), ct));

        [Authorize]
        [HttpPost("admin/pdf-export")]
        public async Task<IActionResult> LogPdfExport([FromBody] AdminSolarExportRequest request, CancellationToken ct)
            => CreateActionResult(await _service.LogPdfExportAsync(request, GetAdminUserId(), ct));

        [HttpPost("public/calculation")]
        [EnableRateLimiting("public-write")]
        public async Task<IActionResult> LogPublicCalculation([FromBody] PublicSolarTrackingRequest request, CancellationToken ct)
            => CreateActionResult(await _service.LogPublicCalculationAsync(request, ct));

        [HttpPost("public/whatsapp-click")]
        [EnableRateLimiting("public-write")]
        public async Task<IActionResult> LogPublicWhatsappClick([FromBody] PublicSolarTrackingRequest request, CancellationToken ct)
            => CreateActionResult(await _service.LogPublicWhatsappClickAsync(request, ct));

        [Authorize]
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
            => CreateActionResult(await _service.GetDashboardAsync(from, to, ct));

        private int? GetAdminUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out var adminUserId) ? adminUserId : null;
        }
    }
}
