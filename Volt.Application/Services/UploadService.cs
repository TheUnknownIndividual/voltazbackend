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

                var dto = new UploadImageDto(request.File.FileName, path);
                return ApiResponse<UploadImageDto>.SuccessResponse(dto);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("extension", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<UploadImageDto>.ErrorResponse(
                    ErrorCode.INVALID_FILE_EXTENSION,
                    ErrorCode.INVALID_FILE_EXTENSION);
            }
            catch
            {
                return ApiResponse<UploadImageDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while uploading the image.");
            }
        }

        public async Task<ApiResponse<UploadPdfDto>> UploadPdfAsync(UploadPdfRequest request, CancellationToken ct = default)
        {
            if (request is null || request.File is null)
            {
                return ApiResponse<UploadPdfDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR,
                    ErrorCode.FILE_REQUIRED);
            }

            try
            {
                var folderName = string.IsNullOrWhiteSpace(request.FolderName)
                    ? "documents"
                    : request.FolderName.Trim().ToLowerInvariant();

                var path = await _fileService.UploadPdfAsync(request.File, folderName, ct);

                var dto = new UploadPdfDto(request.File.FileName, path);
                return ApiResponse<UploadPdfDto>.SuccessResponse(dto);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("extension", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<UploadPdfDto>.ErrorResponse(
                    ErrorCode.INVALID_FILE_EXTENSION,
                    ErrorCode.INVALID_FILE_EXTENSION);
            }
            catch
            {
                return ApiResponse<UploadPdfDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while uploading the PDF.");
            }
        }

        public Task<ApiResponse<NoContentDto>> DeleteImageAsync(string fileUrl, CancellationToken ct = default)
            => DeleteUploadAsync(fileUrl, ct);

        public async Task<ApiResponse<NoContentDto>> DeleteUploadAsync(string fileUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR,
                    ErrorCode.FILE_URL_REQUIRED);
            }

            try
            {
                await _fileService.DeleteFileAsync(fileUrl, ct);
                return ApiResponse<NoContentDto>.SuccessResponse(new NoContentDto());
            }
            catch (FileNotFoundException)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR,
                    "File not found.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("storage", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR,
                    ex.Message);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the file.");
            }
        }
    }
}
