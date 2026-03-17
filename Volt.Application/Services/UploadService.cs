using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;
using Volt.Domain.Common;

namespace Volt.Application.Services
{
    public sealed class UploadService : IUploadService
    {
        private readonly IFileService _fileService;

        public UploadService(IFileService fileService)
        {
            _fileService = fileService;
        }
        public async Task<ApiResponse<UploadImageDto>> UploadImageAsync(UploadImageRequest request, CancellationToken ct = default)
        {
            if (request is null || request.File is null)
            {
                return ApiResponse<UploadImageDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR,
                    ErrorCode.FILE_REQUIRED);
            }

            try
            {
                var folderName = string.IsNullOrWhiteSpace(request.FolderName)
                    ? "common"
                    : request.FolderName.Trim().ToLowerInvariant();

                var path = await _fileService.UploadImageAsync(request.File, folderName, ct);

                var dto = new UploadImageDto(
                    request.File.FileName,
                    path);

                return ApiResponse<UploadImageDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<UploadImageDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while uploading the image.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteImageAsync(string request, CancellationToken ct = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR,
                    ErrorCode.FILE_URL_REQUIRED);
            }

            try
            {
                await _fileService.DeleteFileAsync(request);
                return ApiResponse<NoContentDto>.SuccessResponse(new NoContentDto());
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the image.");
            }
        }
    }
}
