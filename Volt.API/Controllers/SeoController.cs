using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers
{
    [Route("api/seo")]
    [ApiController]
    public sealed class SeoController : ControllerBase
    {
        private readonly ISeoFeedService _feedService;
        private readonly IOptionsMonitor<SeoOptions> _options;

        public SeoController(ISeoFeedService feedService, IOptionsMonitor<SeoOptions> options)
        {
            _feedService = feedService;
            _options = options;
        }

        [HttpGet("sitemap.xml")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Sitemap(CancellationToken ct)
            => Content(
                await _feedService.GenerateSitemapXmlAsync(ct),
                "application/xml; charset=utf-8",
                Encoding.UTF8);

        [HttpGet("robots.txt")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public IActionResult Robots()
            => Content(
                _feedService.GenerateRobotsTxt(),
                "text/plain; charset=utf-8",
                Encoding.UTF8);

        [HttpGet("indexnow-key.txt")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public IActionResult IndexNowKey()
        {
            var key = _options.CurrentValue.IndexNowKey?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return NotFound();
            }

            return Content(key, "text/plain; charset=utf-8", Encoding.UTF8);
        }
    }
}
