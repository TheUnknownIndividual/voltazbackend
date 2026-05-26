using Volt.Application.Dtos;

namespace Volt.Application.Interfaces
{
    public interface IUploadService
    {
        Task<ApiResponse<UploadImageDto>> UploadImageAsync(UploadImageRequest request, CancellationToken ct = default);
        Task<ApiResponse<UploadPdfDto>> UploadPdfAsync(UploadPdfRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteImageAsync(string fileUrl, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteUploadAsync(string fileUrl, CancellationToken ct = default);
    }
}
