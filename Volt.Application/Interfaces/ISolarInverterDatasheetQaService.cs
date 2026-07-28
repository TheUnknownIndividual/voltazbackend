#nullable enable

using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarInverter;

namespace Volt.Application.Interfaces;

public interface ISolarInverterDatasheetQaService
{
    Task<ApiResponse<SolarInverterQaListDto>> GetListAsync(
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ApiResponse<SolarInverterQaDetailDto>> GetDetailAsync(
        int specificationId,
        CancellationToken ct = default);

    Task<ApiResponse<SolarInverterQaDetailDto>> UpdateAsync(
        int specificationId,
        int adminUserId,
        SolarInverterQaUpdateRequest request,
        CancellationToken ct = default);

    Task<ApiResponse<SolarInverterQaDoneDto>> DoneAsync(
        int specificationId,
        int adminUserId,
        CancellationToken ct = default);
}
