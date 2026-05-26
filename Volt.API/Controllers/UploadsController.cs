using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.API.Models;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;
using Volt.Domain.Common;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadsController : CustomBaseController
    {
        private readonly IUploadService _uploadService;

        public UploadsController(IUploadService uploadService)
        {
            _uploadService = uploadService;
        }

        [Authorize]
        [HttpPost("image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] UploadImageFormRequest form, CancellationToken ct)
        {
            if (form?.File is null)
            {
                return CreateActionResult(
                    ApiResponse<UploadImageDto>.ErrorResponse(
                        ErrorCode.VALIDATION_ERROR,
                        ErrorCode.FILE_REQUIRED));
            }

            var memoryStream = new MemoryStream();
            await form.File.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            var request = new UploadImageRequest
            {
                FolderName = "common",
                File = new FileUploadRequest
                {
                    FileName = form.File.FileName,
                    Content = memoryStream
                }
            };

            return CreateActionResult(await _uploadService.UploadImageAsync(request, ct));
        }

        [Authorize]
        [HttpPost("pdf")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPdf([FromForm] UploadPdfFormRequest form, CancellationToken ct)
        {
            if (form?.File is null)
            {
                return CreateActionResult(
                    ApiResponse<UploadPdfDto>.ErrorResponse(
                        ErrorCode.VALIDATION_ERROR,
                        ErrorCode.FILE_REQUIRED));
            }

            var memoryStream = new MemoryStream();
            await form.File.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            var request = new UploadPdfRequest
            {
                FolderName = "documents",
                File = new FileUploadRequest
                {
                    FileName = form.File.FileName,
                    Content = memoryStream
                }
            };

            return CreateActionResult(await _uploadService.UploadPdfAsync(request, ct));
        }

        [Authorize]
        [HttpDelete("image")]
        public async Task<IActionResult> DeleteImage([FromQuery] string fileUrl, CancellationToken ct)
            => CreateActionResult(await _uploadService.DeleteImageAsync(fileUrl, ct));

        [Authorize]
        [HttpDelete]
        public async Task<IActionResult> DeleteUpload([FromQuery] string fileUrl, CancellationToken ct)
            => CreateActionResult(await _uploadService.DeleteUploadAsync(fileUrl, ct));
    }
}
