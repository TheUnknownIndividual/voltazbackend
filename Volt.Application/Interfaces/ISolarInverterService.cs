using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarInverter;

namespace Volt.Application.Interfaces;

public interface ISolarInverterService
{
    Task<ApiResponse<IReadOnlyList<SolarInverterDto>>> GetAllAsync(
        string? systemType,
        string? phase,
        CancellationToken ct = default);

    Task<ApiResponse<SolarInverterDatasheetImportReportDto>> ImportDatasheetsAsync(
        SolarInverterDatasheetImportRequest request,
        CancellationToken ct = default);
}
