using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;

namespace Volt.Application.Interfaces
{
    public interface IUploadService
    {
        Task<ApiResponse<UploadImageDto>> UploadImageAsync(UploadImageRequest request, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteImageAsync(string request, CancellationToken ct = default);
    }
}
