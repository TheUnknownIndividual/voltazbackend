using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
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
        [EnableRateLimiting("public-analytics")]
        [RequestSizeLimit(65_536)]
        public async Task<IActionResult> LogPublicWhatsappClick([FromBody] PublicSolarTrackingRequest request, CancellationToken ct)
        {
            request.ClientIpAddress = GetClientAddress(HttpContext);
            request.RequestUserAgent = Request.Headers.UserAgent.ToString();
            request.RequestReferrer = Request.Headers.Referer.ToString();
            return CreateActionResult(await _service.LogPublicWhatsappClickAsync(request, ct));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
            => CreateActionResult(await _service.GetDashboardAsync(from, to, ct));

        private int? GetAdminUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out var adminUserId) ? adminUserId : null;
        }

        private static string GetClientAddress(HttpContext context)
        {
            foreach (var headerName in new[] { "CF-Connecting-IP", "X-Forwarded-For", "X-Real-IP" })
            {
                var rawValue = context.Request.Headers[headerName].FirstOrDefault();
                var candidate = rawValue?
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (IPAddress.TryParse(candidate, out var address))
                {
                    return address.ToString();
                }
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
