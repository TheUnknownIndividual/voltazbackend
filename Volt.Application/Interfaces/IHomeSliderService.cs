using Volt.Application.Dtos;
using Volt.Application.Dtos.HomeSlider;

namespace Volt.Application.Interfaces;

public interface IHomeSliderService
{
    Task<ApiResponse<IReadOnlyList<HomeSlideDto>>> GetAsync(CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<HomeSlideDto>>> UpdateAsync(HomeSliderUpdateRequest request, CancellationToken ct = default);
}
