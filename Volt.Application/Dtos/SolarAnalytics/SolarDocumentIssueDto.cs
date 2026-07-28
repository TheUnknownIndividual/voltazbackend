namespace Volt.Application.Dtos.SolarAnalytics
{
    public sealed record SolarDocumentIssueDto(
        int ProjectId,
        int AdminTrackedProjectId,
        int CalculationLogId,
        int DocumentLogId,
        string DocumentCode,
        string DocumentNumber,
        string VerificationToken,
        string VerificationUrl);
}
