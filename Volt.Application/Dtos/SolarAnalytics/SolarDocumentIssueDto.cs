namespace Volt.Application.Dtos.SolarAnalytics
{
    public sealed record SolarDocumentIssueDto(
        int ProjectId,
        int CalculationLogId,
        int DocumentLogId,
        string DocumentCode,
        string DocumentNumber);
}
