namespace Volt.Application.Dtos.HomeSlider;

public sealed record HomeSlideDto(
    long Id,
    string Title,
    string Subtitle,
    string Image,
    string? MobileImage,
    string? Video,
    string? Cta,
    bool Centered);
