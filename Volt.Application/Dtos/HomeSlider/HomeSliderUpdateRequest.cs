namespace Volt.Application.Dtos.HomeSlider;

public sealed class HomeSliderUpdateRequest
{
    public IReadOnlyList<HomeSlideDto> Slides { get; set; } = Array.Empty<HomeSlideDto>();
}
