namespace Volt.Application.Dtos.SolarAnalytics
{
    public sealed record SolarAnalyticsDashboardDto(
        SolarAnalyticsSummaryDto Summary,
        IReadOnlyList<SolarAnalyticsTimePointDto> TimeSeries,
        IReadOnlyList<SolarAnalyticsBreakdownDto> DocumentsByCode,
        IReadOnlyList<SolarAnalyticsBreakdownDto> SourceBreakdown,
        IReadOnlyList<SolarAnalyticsProjectDto> TopProjects,
        IReadOnlyList<SolarAnalyticsRecentActivityDto> RecentActivity);

    public sealed record SolarAnalyticsSummaryDto(
        int TotalCalculations,
        int WebCalculations,
        int AdminExports,
        int WhatsappClicks,
        int DocumentsIssued,
        int UniqueProjects);

    public sealed record SolarAnalyticsTimePointDto(
        string Date,
        int Calculations,
        int Documents,
        int WhatsappClicks);

    public sealed record SolarAnalyticsBreakdownDto(
        string Key,
        int Count);

    public sealed record SolarAnalyticsProjectDto(
        int ProjectId,
        string ProjectName,
        int CalculationCount,
        int DocumentCount,
        DateTime LastActivityAt);

    public sealed record SolarAnalyticsRecentActivityDto(
        string Kind,
        string Source,
        string EventType,
        string ProjectName,
        string DocumentNumber,
        string DocumentCode,
        DateTime CreatedAt,
        string PayloadJson);
}
