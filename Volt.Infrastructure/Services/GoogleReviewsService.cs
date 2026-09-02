using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Dtos.GoogleReviews;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services;

/// <summary>Fetches and caches reviews from the Google Places API. Never fabricates data —
/// returns an empty result (never an error) when unconfigured or when Google is unreachable,
/// so the homepage simply omits the section rather than showing a broken state.</summary>
public sealed class GoogleReviewsService : IGoogleReviewsService
{
    private const string CacheKey = "volt-google-reviews";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    private readonly HttpClient _client;
    private readonly GoogleReviewsOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GoogleReviewsService> _logger;

    public GoogleReviewsService(HttpClient client, IOptions<GoogleReviewsOptions> options, IMemoryCache cache, ILogger<GoogleReviewsService> logger)
    {
        _client = client;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<GoogleReviewsResultDto>> GetReviewsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.PlaceId))
        {
            return ApiResponse<GoogleReviewsResultDto>.SuccessResponse(new GoogleReviewsResultDto());
        }

        if (_cache.TryGetValue(CacheKey, out GoogleReviewsResultDto cached) && cached is not null)
        {
            return ApiResponse<GoogleReviewsResultDto>.SuccessResponse(cached);
        }

        try
        {
            var url = "maps/api/place/details/json"
                + $"?place_id={Uri.EscapeDataString(_options.PlaceId)}"
                + "&fields=rating,user_ratings_total,reviews,url"
                + "&reviews_no_translations=true"
                + $"&key={Uri.EscapeDataString(_options.ApiKey)}";
            var response = await _client.GetFromJsonAsync<GooglePlaceDetailsResponse>(url, ct);

            if (response is null || response.Status != "OK" || response.Result is null)
            {
                _logger.LogWarning("Google Places request did not return OK. Status={Status}", response?.Status);
                return ApiResponse<GoogleReviewsResultDto>.SuccessResponse(new GoogleReviewsResultDto());
            }

            var result = new GoogleReviewsResultDto
            {
                OverallRating = response.Result.Rating,
                UserRatingCount = response.Result.UserRatingsTotal,
                GoogleMapsUrl = response.Result.Url ?? string.Empty,
                Reviews = (response.Result.Reviews ?? Array.Empty<GooglePlaceReview>())
                    .OrderByDescending(x => x.Time)
                    .Take(5)
                    .Select(x => new GoogleReviewDto
                    {
                        ReviewerName = x.AuthorName ?? string.Empty,
                        ReviewerPhotoUrl = x.ProfilePhotoUrl ?? string.Empty,
                        Rating = x.Rating,
                        Text = x.Text ?? string.Empty,
                        RelativeTime = x.RelativeTimeDescription ?? string.Empty,
                        PublishTime = x.Time,
                    })
                    .ToList(),
            };

            _cache.Set(CacheKey, result, CacheTtl);
            return ApiResponse<GoogleReviewsResultDto>.SuccessResponse(result);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to fetch Google reviews.");
            return ApiResponse<GoogleReviewsResultDto>.SuccessResponse(new GoogleReviewsResultDto());
        }
    }

    private sealed class GooglePlaceDetailsResponse
    {
        [JsonPropertyName("result")]
        public GooglePlaceResult Result { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }
    }

    private sealed class GooglePlaceResult
    {
        [JsonPropertyName("rating")]
        public double? Rating { get; set; }

        [JsonPropertyName("user_ratings_total")]
        public int? UserRatingsTotal { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("reviews")]
        public GooglePlaceReview[] Reviews { get; set; }
    }

    private sealed class GooglePlaceReview
    {
        [JsonPropertyName("author_name")]
        public string AuthorName { get; set; }

        [JsonPropertyName("profile_photo_url")]
        public string ProfilePhotoUrl { get; set; }

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("relative_time_description")]
        public string RelativeTimeDescription { get; set; }

        [JsonPropertyName("time")]
        public long Time { get; set; }
    }
}
