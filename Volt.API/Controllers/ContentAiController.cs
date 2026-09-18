using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Volt.API.Services;
using Volt.Application.Dtos;
using Volt.Application.Dtos.ContentAi;
using Volt.Domain.Common;
using Volt.Domain.Enums;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class ContentAiController : CustomBaseController
    {
        private readonly ContentAiGenerationCoordinator _coordinator;

        public ContentAiController(ContentAiGenerationCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpGet("settings")]
        public IActionResult GetSettings()
            => CreateActionResult(ApiResponse<ContentAiSettingsDto>.SuccessResponse(_coordinator.GetSettings()));

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpPost("generate")]
        [EnableRateLimiting("content-ai-generate")]
        public async Task<IActionResult> StartGeneration(
            [FromBody] ContentAiGenerationStartRequest request,
            CancellationToken ct)
        {
            try
            {
                var job = await _coordinator.StartAsync(GetAdminUserId(), User.FindFirstValue(ClaimTypes.Name), request, ct);
                return CreateActionResult(ApiResponse<ContentAiJobDto>.SuccessResponse(job));
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
            {
                return CreateActionResult(ApiResponse<ContentAiJobDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, ex.Message));
            }
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpGet("{jobId:guid}")]
        public async Task<IActionResult> GetGeneration(Guid jobId, CancellationToken ct)
        {
            var job = await _coordinator.GetAsync(GetAdminUserId(), jobId, ct);
            return job is null
                ? CreateActionResult(ApiResponse<ContentAiJobDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "CONTENT_AI_JOB_NOT_FOUND"))
                : CreateActionResult(ApiResponse<ContentAiJobDto>.SuccessResponse(job));
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpDelete("{jobId:guid}")]
        public async Task<IActionResult> CancelGeneration(Guid jobId, CancellationToken ct)
        {
            var job = await _coordinator.CancelAsync(GetAdminUserId(), User.FindFirstValue(ClaimTypes.Name), jobId, ct);
            return job is null
                ? CreateActionResult(ApiResponse<ContentAiJobDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "CONTENT_AI_JOB_NOT_FOUND"))
                : CreateActionResult(ApiResponse<ContentAiJobDto>.SuccessResponse(job));
        }

        private int GetAdminUserId()
            => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ? adminId : 0;
    }
}
