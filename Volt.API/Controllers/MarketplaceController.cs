using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.API.Services;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Marketplace;
using Volt.Domain.Common;
using Volt.Domain.Enums;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class MarketplaceController : CustomBaseController
    {
        private readonly MarketplaceListingService _service;

        public MarketplaceController(MarketplaceListingService service)
        {
            _service = service;
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpGet("settings")]
        public IActionResult GetSettings()
            => CreateActionResult(ApiResponse<MarketplaceSettingsDto>.SuccessResponse(_service.GetSettings()));

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpPost("prepare")]
        [EnableRateLimiting("marketplace-prepare")]
        public async Task<IActionResult> Prepare([FromBody] MarketplacePrepareRequest request, CancellationToken ct)
        {
            try
            {
                var batch = await _service.PrepareBatchAsync(request, ct);
                return CreateActionResult(ApiResponse<MarketplaceBatchDto>.SuccessResponse(batch));
            }
            catch (InvalidOperationException ex)
            {
                return CreateActionResult(ApiResponse<MarketplaceBatchDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, ex.Message));
            }
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpGet("listings")]
        public async Task<IActionResult> GetListings([FromQuery] string? productIds, CancellationToken ct)
        {
            var ids = (productIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .ToList();
            var listings = await _service.GetListingsAsync(ids, ct);
            return CreateActionResult(ApiResponse<List<MarketplaceListingDto>>.SuccessResponse(listings));
        }

        // Called by the browser helper; the random, short-lived code is the credential.
        [AllowAnonymous]
        [HttpGet("payload/{code}")]
        [EnableRateLimiting("marketplace-payload")]
        public IActionResult GetPayload(string code)
        {
            var envelope = _service.GetEnvelope(code);
            return envelope is null
                ? CreateActionResult(ApiResponse<MarketplacePayloadEnvelope>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, "MARKETPLACE_PAYLOAD_NOT_FOUND_OR_EXPIRED"))
                : CreateActionResult(ApiResponse<MarketplacePayloadEnvelope>.SuccessResponse(envelope));
        }

        [AllowAnonymous]
        [HttpPost("report/{code}")]
        [EnableRateLimiting("marketplace-payload")]
        public async Task<IActionResult> Report(string code, [FromBody] MarketplaceReportRequest request, CancellationToken ct)
        {
            try
            {
                var ok = await _service.ReportAsync(code, request, ct);
                return ok
                    ? CreateActionResult(ApiResponse<bool>.SuccessResponse(true))
                    : CreateActionResult(ApiResponse<bool>.ErrorResponse(
                        ErrorCode.VALIDATION_ERROR, "MARKETPLACE_PAYLOAD_NOT_FOUND_OR_EXPIRED"));
            }
            catch (InvalidOperationException ex)
            {
                return CreateActionResult(ApiResponse<bool>.ErrorResponse(ErrorCode.VALIDATION_ERROR, ex.Message));
            }
        }
    }
}
