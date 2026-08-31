using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using System.Security.Claims;
using Volt.API.Models;
using Volt.API.Services;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Product;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Enums;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class ProductsController : CustomBaseController
    {
        private readonly IProductService _service;
        private readonly ProductDatasheetUploadService _datasheetUploads;
        private readonly ProductAiImportCoordinator _aiImports;
        private readonly IHttpClientFactory _httpClientFactory;

        public ProductsController(
            IProductService service,
            ProductDatasheetUploadService datasheetUploads,
            ProductAiImportCoordinator aiImports,
            IHttpClientFactory httpClientFactory)
        {
            _service = service;
            _datasheetUploads = datasheetUploads;
            _aiImports = aiImports;
            _httpClientFactory = httpClientFactory;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductCreateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.CreateAsync(request, ct));

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] ProductFiltrDto dto, CancellationToken ct)
            => CreateActionResult(await _service.GetAllAsync(dto, ct));

        [HttpGet("HomePage")]
        public async Task<IActionResult> GetAllForHomePage([FromQuery] ProductFiltrHomePageDto dto ,CancellationToken ct)
            => CreateActionResult(await _service.GetAllForHomePageAsync(dto, ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
            => CreateActionResult(await _service.GetByIdAsync(id, ct));

        [HttpGet("datasheets/preview")]
        [EnableRateLimiting("datasheet-preview")]
        public async Task<IActionResult> PreviewDatasheet([FromQuery] string url, CancellationToken ct)
        {
            if (!TryValidateDatasheetUrl(url, out var source)) return BadRequest();

            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            if (Request.Headers.TryGetValue(HeaderNames.Range, out var range))
                request.Headers.TryAddWithoutValidation(HeaderNames.Range, range.ToString());

            HttpResponseMessage upstream;
            try
            {
                upstream = await _httpClientFactory
                    .CreateClient("product-datasheet-preview")
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout);
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status502BadGateway);
            }

            using (upstream)
            {
                if (upstream.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                    return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
                if (upstream.StatusCode is not System.Net.HttpStatusCode.OK and not System.Net.HttpStatusCode.PartialContent)
                    return StatusCode(StatusCodes.Status502BadGateway);

                var mediaType = upstream.Content.Headers.ContentType?.MediaType;
                if (!string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                    return StatusCode(StatusCodes.Status415UnsupportedMediaType);

                const long maxPdfBytes = 50L * 1024 * 1024;
                var totalLength = upstream.Content.Headers.ContentRange?.Length ?? upstream.Content.Headers.ContentLength;
                if (totalLength is > maxPdfBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);

                Response.StatusCode = (int)upstream.StatusCode;
                Response.ContentType = "application/pdf";
                Response.Headers[HeaderNames.CacheControl] = "public,max-age=86400";
                Response.Headers[HeaderNames.ContentDisposition] = "inline";
                Response.Headers[HeaderNames.AcceptRanges] = "bytes";
                if (upstream.Content.Headers.ContentRange is not null)
                    Response.Headers[HeaderNames.ContentRange] = upstream.Content.Headers.ContentRange.ToString();
                if (upstream.Content.Headers.ContentLength.HasValue)
                    Response.ContentLength = upstream.Content.Headers.ContentLength.Value;
                if (upstream.Headers.ETag is not null)
                    Response.Headers[HeaderNames.ETag] = upstream.Headers.ETag.ToString();

                await using var stream = await upstream.Content.ReadAsStreamAsync(ct);
                await stream.CopyToAsync(Response.Body, 81_920, ct);
                return new EmptyResult();
            }
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateRequest request, CancellationToken ct)
            => CreateActionResult(await _service.UpdateAsync(id, request, ct));

        [Authorize]
        [HttpPut("ShowHomePage")]
        public async Task<IActionResult> ShowHomePage(ProductShowHomePageDto dto, CancellationToken ct)
            => CreateActionResult(await _service.ShowHomePage(dto, ct));

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => CreateActionResult(await _service.DeleteAsync(id, ct));

        [Authorize]
        [HttpGet("ShowHomePageProductCount")]
        public async Task<IActionResult> ShowHomePageProductCount(CancellationToken ct)
            => CreateActionResult(await _service.ShowHomePageProductCount(ct));

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpPost("datasheets/upload")]
        [EnableRateLimiting("protected-write")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(57_671_680)]
        public async Task<IActionResult> UploadDatasheets(
            [FromForm] ProductDatasheetUploadFormRequest form,
            CancellationToken ct)
        {
            try
            {
                var uploaded = await _datasheetUploads.UploadAsync(form?.Files ?? [], ct);
                return CreateActionResult(ApiResponse<IReadOnlyList<ProductDatasheetUploadDto>>.SuccessResponse(uploaded));
            }
            catch (InvalidDataException ex)
            {
                return CreateActionResult(ApiResponse<IReadOnlyList<ProductDatasheetUploadDto>>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, ex.Message));
            }
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpGet("ai-imports/settings")]
        public IActionResult GetAiImportSettings()
            => CreateActionResult(ApiResponse<ProductAiImportSettingsDto>.SuccessResponse(_aiImports.GetSettings()));

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpPost("ai-imports")]
        [EnableRateLimiting("product-ai-import")]
        public async Task<IActionResult> StartAiImport(
            [FromBody] ProductAiImportStartRequest request,
            CancellationToken ct)
        {
            try
            {
                var job = await _aiImports.StartAsync(GetAdminUserId(), User.FindFirstValue(ClaimTypes.Name), request, ct);
                return CreateActionResult(ApiResponse<ProductAiImportJobDto>.SuccessResponse(job));
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
            {
                return CreateActionResult(ApiResponse<ProductAiImportJobDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, ex.Message));
            }
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpGet("ai-imports/{jobId:guid}")]
        public async Task<IActionResult> GetAiImport(Guid jobId, CancellationToken ct)
        {
            var job = await _aiImports.GetAsync(GetAdminUserId(), jobId, ct);
            return job is null
                ? CreateActionResult(ApiResponse<ProductAiImportJobDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "PRODUCT_AI_JOB_NOT_FOUND"))
                : CreateActionResult(ApiResponse<ProductAiImportJobDto>.SuccessResponse(job));
        }

        [Authorize(Roles = nameof(Role.Admin))]
        [HttpDelete("ai-imports/{jobId:guid}")]
        public async Task<IActionResult> CancelAiImport(Guid jobId, CancellationToken ct)
        {
            var job = await _aiImports.CancelAsync(GetAdminUserId(), User.FindFirstValue(ClaimTypes.Name), jobId, ct);
            return job is null
                ? CreateActionResult(ApiResponse<ProductAiImportJobDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "PRODUCT_AI_JOB_NOT_FOUND"))
                : CreateActionResult(ApiResponse<ProductAiImportJobDto>.SuccessResponse(job));
        }

        private int GetAdminUserId()
            => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ? adminId : 0;

        private static bool TryValidateDatasheetUrl(string? value, out Uri source)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
                && parsed.Scheme == Uri.UriSchemeHttps
                && parsed.Host.Equals("cloudfiles.volt.az", StringComparison.OrdinalIgnoreCase)
                && parsed.IsDefaultPort
                && string.IsNullOrEmpty(parsed.UserInfo)
                && parsed.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                source = parsed;
                return true;
            }

            source = null!;
            return false;
        }
    }
}
