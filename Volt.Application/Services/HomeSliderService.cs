using System.Text.Json;
using Volt.Application.Dtos;
using Volt.Application.Dtos.HomeSlider;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services;

public sealed class HomeSliderService : IHomeSliderService
{
    private const int SettingsId = 1;
    private const int MaximumSlides = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<HomeSlideDto> DefaultSlides = new[]
    {
        new HomeSlideDto(1, "solar enerji", "", "/sliderphoto.png", "/sliderphotomobile.png", null, "Ətraflı Öyrən", true),
        new HomeSlideDto(2, "enerji qənaəti", "", "/sliderphoto2.png", "/sliderphotomobile2.png", null, "Ətraflı Öyrən", true),
    };

    private readonly IUnitOfWork _uow;

    public HomeSliderService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<IReadOnlyList<HomeSlideDto>>> GetAsync(CancellationToken ct = default)
    {
        var settings = await _uow.Repository<HomeSliderSettings>()
            .FirstOrDefaultNoTrackingAsync(x => x.Id == SettingsId, ct);

        if (settings is null)
        {
            return ApiResponse<IReadOnlyList<HomeSlideDto>>.SuccessResponse(DefaultSlides);
        }

        try
        {
            var slides = JsonSerializer.Deserialize<List<HomeSlideDto>>(settings.SlidesJson, JsonOptions) ?? new();
            return ApiResponse<IReadOnlyList<HomeSlideDto>>.SuccessResponse(slides.Take(MaximumSlides).ToList());
        }
        catch (JsonException)
        {
            return ApiResponse<IReadOnlyList<HomeSlideDto>>.SuccessResponse(DefaultSlides);
        }
    }

    public async Task<ApiResponse<IReadOnlyList<HomeSlideDto>>> UpdateAsync(
        HomeSliderUpdateRequest request,
        CancellationToken ct = default)
    {
        if (request?.Slides is null || request.Slides.Count == 0 || request.Slides.Count > MaximumSlides)
        {
            return ApiResponse<IReadOnlyList<HomeSlideDto>>.ErrorResponse(
                ErrorCode.VALIDATION_ERROR,
                $"Homepage sliders require between 1 and {MaximumSlides} slides.");
        }

        var normalized = request.Slides
            .Select(slide => Normalize(slide))
            .ToList();

        if (normalized.Any(slide => string.IsNullOrWhiteSpace(slide.Title) || string.IsNullOrWhiteSpace(slide.Image)))
        {
            return ApiResponse<IReadOnlyList<HomeSlideDto>>.ErrorResponse(
                ErrorCode.VALIDATION_ERROR,
                "Every slide requires a title and desktop image.");
        }

        try
        {
            var repo = _uow.Repository<HomeSliderSettings>();
            var settings = await repo.FirstOrDefaultAsync(x => x.Id == SettingsId, ct);
            if (settings is null)
            {
                settings = new HomeSliderSettings { Id = SettingsId };
                await repo.AddAsync(settings, ct);
            }

            settings.SlidesJson = JsonSerializer.Serialize(normalized, JsonOptions);
            settings.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<IReadOnlyList<HomeSlideDto>>.SuccessResponse(normalized);
        }
        catch
        {
            return ApiResponse<IReadOnlyList<HomeSlideDto>>.ErrorResponse(
                ErrorCode.SERVER_ERROR,
                "An error occurred while saving homepage sliders.");
        }
    }

    private static HomeSlideDto Normalize(HomeSlideDto slide) => new(
        slide.Id > 0 ? slide.Id : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Crop(slide.Title, 200),
        Crop(slide.Subtitle, 500),
        Crop(slide.Image, 1000),
        Optional(slide.MobileImage, 1000),
        Optional(slide.Video, 1000),
        Optional(slide.Cta, 100),
        slide.Centered);

    private static string Crop(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var normalized = Crop(value, maximumLength);
        return normalized.Length == 0 ? null : normalized;
    }
}
