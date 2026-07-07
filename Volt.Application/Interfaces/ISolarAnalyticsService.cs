using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarAnalytics;

namespace Volt.Application.Interfaces
{
    public interface ISolarAnalyticsService
    {
        Task<ApiResponse<IReadOnlyList<SolarProjectDto>>> SearchProjectsAsync(string? query, CancellationToken ct = default);
        Task<ApiResponse<SolarDocumentIssueDto>> IssueDocxExportAsync(AdminSolarDocxExportRequest request, int? adminUserId, CancellationToken ct = default);
        Task<ApiResponse<SolarCalculationLogDto>> LogPdfExportAsync(AdminSolarExportRequest request, int? adminUserId, CancellationToken ct = default);
        Task<ApiResponse<SolarCalculationLogDto>> LogPublicCalculationAsync(PublicSolarTrackingRequest request, CancellationToken ct = default);
        Task<ApiResponse<SolarCalculationLogDto>> LogPublicWhatsappClickAsync(PublicSolarTrackingRequest request, CancellationToken ct = default);
        Task<ApiResponse<SolarAnalyticsDashboardDto>> GetDashboardAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    }
}
