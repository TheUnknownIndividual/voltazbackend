using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volt.API.Services;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Lalafo;
using Volt.Domain.Common;
using Volt.Domain.Enums;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class LalafoController : CustomBaseController
    {
        private readonly LalafoListingService _service;

        public LalafoController(LalafoListingService service)
        {
            _service = service;
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpGet("settings")]
        public IActionResult GetSettings()
            => CreateActionResult(ApiResponse<LalafoSettingsDto>.SuccessResponse(_service.GetSettings()));

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpPost("prepare")]
        [EnableRateLimiting("lalafo-prepare")]
        public async Task<IActionResult> Prepare([FromBody] LalafoPrepareRequest request, CancellationToken ct)
        {
            try
            {
                var prepared = await _service.PrepareAsync(request.ProductId, ct);
                return CreateActionResult(ApiResponse<LalafoPreparedListingDto>.SuccessResponse(prepared));
            }
            catch (InvalidOperationException ex)
            {
                return CreateActionResult(ApiResponse<LalafoPreparedListingDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, ex.Message));
            }
        }

        // Fetched by the Lalafo helper extension; the random, short-lived code is the credential.
        [AllowAnonymous]
        [HttpGet("payload/{code}")]
        [EnableRateLimiting("lalafo-payload")]
        public IActionResult GetPayload(string code)
        {
            var payload = _service.GetPayload(code);
            return payload is null
                ? CreateActionResult(ApiResponse<LalafoListingPayload>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, "LALAFO_PAYLOAD_NOT_FOUND_OR_EXPIRED"))
                : CreateActionResult(ApiResponse<LalafoListingPayload>.SuccessResponse(payload));
        }
    }
}
