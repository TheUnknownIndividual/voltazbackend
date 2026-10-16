using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarYield;

namespace Volt.Application.Interfaces
{
    public interface ISolarYieldService
    {
        /// <summary>Annual specific yield for a location. When <paramref name="tilt"/> is null the
        /// optimal tilt/azimuth are used; <paramref name="azimuth"/> uses the PVGIS convention (0 = south).</summary>
        Task<ApiResponse<SolarYieldDto>> GetYieldAsync(double latitude, double longitude, int? tilt, int? azimuth, CancellationToken ct = default);
    }
}
