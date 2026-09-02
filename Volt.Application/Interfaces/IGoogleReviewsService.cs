using Volt.Application.Dtos;
using Volt.Application.Dtos.GoogleReviews;

namespace Volt.Application.Interfaces
{
    public interface IGoogleReviewsService
    {
        Task<ApiResponse<GoogleReviewsResultDto>> GetReviewsAsync(CancellationToken ct = default);
    }
}
