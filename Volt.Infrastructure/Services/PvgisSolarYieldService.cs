#nullable enable
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarYield;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services;

/// <summary>Proxies the EU JRC PVGIS PVcalc API (which does not allow browser calls) and caches the
/// result. Irradiation data is a multi-year climatology that only changes when JRC ships a new
/// database, so results are cached for a long time; identical concurrent requests share one upstream
/// call. On failure an error is returned so the calculators fall back to their bundled PVGIS table.</summary>
public sealed class PvgisSolarYieldService : ISolarYieldService
{
    // PVGIS default system loss (cables, inverter, soiling, mismatch). Temperature, spectral and
    // reflection losses are modelled by PVGIS itself on top of this.
    private const double SystemLossPercent = 14;

    // Azerbaijan incl. Naxçıvan, with a small margin. Keeps this from being an open PVGIS proxy.
    private const double MinLatitude = 38.2, MaxLatitude = 42.0, MinLongitude = 44.6, MaxLongitude = 51.0;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan OutageBackoff = TimeSpan.FromMinutes(2);
    private const string OutageCacheKey = "pvgis:outage";

    // PVGIS allows ~30 req/s per IP; stay far below that.
    private static readonly SemaphoreSlim UpstreamGate = new(4, 4);
    private static readonly ConcurrentDictionary<string, Task<SolarYieldDto?>> InFlight = new();

    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PvgisSolarYieldService> _logger;

    public PvgisSolarYieldService(HttpClient client, IMemoryCache cache, ILogger<PvgisSolarYieldService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<SolarYieldDto>> GetYieldAsync(double latitude, double longitude, int? tilt, int? azimuth, CancellationToken ct = default)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude)
            || latitude < MinLatitude || latitude > MaxLatitude
            || longitude < MinLongitude || longitude > MaxLongitude)
        {
            return ApiResponse<SolarYieldDto>.ErrorResponse("LOCATION_OUT_OF_RANGE", null);
        }

        if (tilt is < 0 or > 90 || azimuth is < -180 or > 180)
        {
            return ApiResponse<SolarYieldDto>.ErrorResponse("INVALID_ANGLES", null);
        }

        // ~1 km grid: well inside PVGIS' own data resolution and keeps the cache small.
        var lat = Math.Round(latitude, 2);
        var lon = Math.Round(longitude, 2);
        var optimal = tilt is null;
        var aspect = optimal ? (int?)null : azimuth ?? 0;
        var cacheKey = string.Create(CultureInfo.InvariantCulture, $"pvgis:{lat:F2}:{lon:F2}:{(optimal ? "opt" : $"{tilt}:{aspect}")}");

        if (_cache.TryGetValue(cacheKey, out SolarYieldDto? cached) && cached is not null)
        {
            return ApiResponse<SolarYieldDto>.SuccessResponse(cached);
        }

        if (_cache.TryGetValue(OutageCacheKey, out _))
        {
            return ApiResponse<SolarYieldDto>.ErrorResponse("PVGIS_UNAVAILABLE", null);
        }

        var result = await InFlight.GetOrAdd(cacheKey, key => FetchAndCacheAsync(key, lat, lon, tilt, aspect)).WaitAsync(ct);
        return result is null
            ? ApiResponse<SolarYieldDto>.ErrorResponse("PVGIS_UNAVAILABLE", null)
            : ApiResponse<SolarYieldDto>.SuccessResponse(result);
    }

    private async Task<SolarYieldDto?> FetchAndCacheAsync(string cacheKey, double lat, double lon, int? tilt, int? aspect)
    {
        // Guarantees GetOrAdd has stored this task before the finally block can remove it.
        await Task.Yield();
        try
        {
            var result = await FetchAsync(lat, lon, tilt, aspect);
            if (result is not null)
            {
                _cache.Set(cacheKey, result, CacheTtl);
            }
            return result;
        }
        finally
        {
            InFlight.TryRemove(cacheKey, out _);
        }
    }

    // Not tied to the caller's CancellationToken: other requests may be awaiting the same task,
    // and the HttpClient timeout bounds it anyway.
    private async Task<SolarYieldDto?> FetchAsync(double lat, double lon, int? tilt, int? aspect)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"api/v5_3/PVcalc?lat={lat:F4}&lon={lon:F4}&peakpower=1&loss={SystemLossPercent}&usehorizon=1&outputformat=json")
            + (tilt is null
                ? "&optimalangles=1"
                : string.Create(CultureInfo.InvariantCulture, $"&angle={tilt}&aspect={aspect}"));

        await UpstreamGate.WaitAsync();
        try
        {
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PVGIS returned {StatusCode} for {Url}", (int)response.StatusCode, url);
                if ((int)response.StatusCode >= 500 || (int)response.StatusCode == 429)
                {
                    _cache.Set(OutageCacheKey, true, OutageBackoff);
                }
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;
            var fixedTotals = root.GetProperty("outputs").GetProperty("totals").GetProperty("fixed");
            var mounting = root.GetProperty("inputs").GetProperty("mounting_system").GetProperty("fixed");
            var yearlyEnergy = fixedTotals.GetProperty("E_y").GetDouble();
            if (!double.IsFinite(yearlyEnergy) || yearlyEnergy <= 0)
            {
                return null;
            }

            return new SolarYieldDto
            {
                Latitude = lat,
                Longitude = lon,
                Tilt = mounting.GetProperty("slope").GetProperty("value").GetDouble(),
                Azimuth = mounting.GetProperty("azimuth").GetProperty("value").GetDouble(),
                OptimalAngles = tilt is null,
                SystemLossPercent = SystemLossPercent,
                SpecificYieldKwhPerKwp = Math.Round(yearlyEnergy),
                RadiationDatabase = root.GetProperty("inputs").GetProperty("meteo_data").GetProperty("radiation_db").GetString() ?? string.Empty,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "PVGIS request failed for {Url}", url);
            _cache.Set(OutageCacheKey, true, OutageBackoff);
            return null;
        }
        finally
        {
            UpstreamGate.Release();
        }
    }
}
