using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarAnalytics;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services
{
    public sealed class SolarAnalyticsService : ISolarAnalyticsService
    {
        private const int FirstDocumentNumber = 313;
        private const string SourceAdmin = "Admin";
        private const string SourceWeb = "Web";
        private const string EventAdminDocx = "ADMIN_DOCX_EXPORT";
        private const string EventAdminPdf = "ADMIN_PDF_EXPORT";
        private const string EventWebCalculation = "WEB_CALCULATION";
        private const string EventWebWhatsappClick = "WEB_WHATSAPP_CLICK";

        private static readonly HashSet<string> AllowedDocumentCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CP", "QUO", "INV", "PI", "CTR", "AGR", "PO", "SO", "BOQ", "PRJ", "DRW", "REP", "ACT", "LET", "MEM", "SPEC", "CAL"
        };

        private readonly DataContext _context;

        public SolarAnalyticsService(DataContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<IReadOnlyList<SolarProjectDto>>> SearchProjectsAsync(string? query, CancellationToken ct = default)
        {
            var normalizedQuery = NormalizeName(query ?? string.Empty);
            var projectsQuery = _context.SolarSalesProjects.AsNoTracking().Where(x => x.IsActive);

            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                projectsQuery = projectsQuery.Where(x => x.NormalizedName.Contains(normalizedQuery));
            }

            var projects = await projectsQuery
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenBy(x => x.Name)
                .Take(12)
                .Select(x => new SolarProjectDto(x.Id, x.Name, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(ct);

            return ApiResponse<IReadOnlyList<SolarProjectDto>>.SuccessResponse(projects);
        }

        public async Task<ApiResponse<SolarDocumentIssueDto>> IssueDocxExportAsync(AdminSolarDocxExportRequest request, int? adminUserId, CancellationToken ct = default)
        {
            var validation = ValidateProjectName(request.ProjectName) ?? ValidateDocumentCode(request.DocumentCode);
            if (validation is not null)
            {
                return ApiResponse<SolarDocumentIssueDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, validation);
            }

            var now = GetAzerbaijanNow();
            var documentCode = request.DocumentCode.Trim().ToUpperInvariant();
            var payloadJson = ToPayloadJson(request.Payload);

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                var project = await ResolveProjectAsync(request.ProjectName, now, ct);
                var calculationLog = CreateCalculationLog(
                    SourceAdmin,
                    EventAdminDocx,
                    project.Id,
                    adminUserId,
                    request.Language,
                    request.SessionId,
                    payloadJson,
                    now);

                _context.SolarCalculationLogs.Add(calculationLog);
                await _context.SaveChangesAsync(ct);

                var sequence = await _context.DocumentSequences
                    .FirstOrDefaultAsync(x => x.DocumentCode == documentCode && x.Year == now.Year && x.Month == now.Month, ct);

                if (sequence is null)
                {
                    sequence = new DocumentSequence
                    {
                        DocumentCode = documentCode,
                        Year = now.Year,
                        Month = now.Month,
                        CurrentNumber = FirstDocumentNumber - 1,
                        CreatedAt = now
                    };
                    _context.DocumentSequences.Add(sequence);
                }

                sequence.CurrentNumber += 1;
                sequence.UpdatedAt = now;

                var documentNumber = $"VOLT-{documentCode}-{now:yyyy-MM}-{sequence.CurrentNumber:D3}";
                var documentLog = new DocumentLog
                {
                    DocumentCode = documentCode,
                    DocumentNumber = documentNumber,
                    SolarSalesProjectId = project.Id,
                    SolarCalculationLogId = calculationLog.Id,
                    AdminUserId = adminUserId,
                    PayloadJson = payloadJson,
                    CreatedAt = now
                };

                _context.DocumentLogs.Add(documentLog);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return ApiResponse<SolarDocumentIssueDto>.SuccessResponse(new SolarDocumentIssueDto(
                    project.Id,
                    calculationLog.Id,
                    documentLog.Id,
                    documentCode,
                    documentNumber));
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                return ApiResponse<SolarDocumentIssueDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while issuing the document number.");
            }
        }

        public async Task<ApiResponse<SolarCalculationLogDto>> LogPdfExportAsync(AdminSolarExportRequest request, int? adminUserId, CancellationToken ct = default)
        {
            var validation = ValidateProjectName(request.ProjectName);
            if (validation is not null)
            {
                return ApiResponse<SolarCalculationLogDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, validation);
            }

            try
            {
                var now = GetAzerbaijanNow();
                var project = await ResolveProjectAsync(request.ProjectName, now, ct);
                var calculationLog = CreateCalculationLog(
                    SourceAdmin,
                    EventAdminPdf,
                    project.Id,
                    adminUserId,
                    request.Language,
                    request.SessionId,
                    ToPayloadJson(request.Payload),
                    now);

                _context.SolarCalculationLogs.Add(calculationLog);
                await _context.SaveChangesAsync(ct);

                return ApiResponse<SolarCalculationLogDto>.SuccessResponse(new SolarCalculationLogDto(project.Id, calculationLog.Id));
            }
            catch
            {
                return ApiResponse<SolarCalculationLogDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while logging the PDF export.");
            }
        }

        public Task<ApiResponse<SolarCalculationLogDto>> LogPublicCalculationAsync(PublicSolarTrackingRequest request, CancellationToken ct = default)
            => LogPublicEventAsync(request, EventWebCalculation, ct);

        public Task<ApiResponse<SolarCalculationLogDto>> LogPublicWhatsappClickAsync(PublicSolarTrackingRequest request, CancellationToken ct = default)
            => LogPublicEventAsync(request, EventWebWhatsappClick, ct);

        public async Task<ApiResponse<SolarAnalyticsDashboardDto>> GetDashboardAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var now = GetAzerbaijanNow();
            var start = from ?? now.AddDays(-29).Date;
            var end = (to ?? now).Date.AddDays(1).AddTicks(-1);

            var logs = await _context.SolarCalculationLogs
                .AsNoTracking()
                .Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
                .ToListAsync(ct);
            var documents = await _context.DocumentLogs
                .AsNoTracking()
                .Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
                .ToListAsync(ct);
            var projects = await _context.SolarSalesProjects
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

            var days = Enumerable.Range(0, Math.Max(1, (end.Date - start.Date).Days + 1))
                .Select(offset => start.Date.AddDays(offset))
                .ToList();

            var timeSeries = days.Select(day =>
            {
                var next = day.AddDays(1);
                return new SolarAnalyticsTimePointDto(
                    day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    logs.Count(x => x.CreatedAt >= day && x.CreatedAt < next && x.EventType != EventWebWhatsappClick),
                    documents.Count(x => x.CreatedAt >= day && x.CreatedAt < next),
                    logs.Count(x => x.CreatedAt >= day && x.CreatedAt < next && x.EventType == EventWebWhatsappClick));
            }).ToList();

            var projectIds = logs.Select(x => x.SolarSalesProjectId)
                .Concat(documents.Select(x => (int?)x.SolarSalesProjectId))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var topProjects = projectIds
                .Select(projectId =>
                {
                    var projectLogs = logs.Where(x => x.SolarSalesProjectId == projectId).ToList();
                    var projectDocuments = documents.Where(x => x.SolarSalesProjectId == projectId).ToList();
                    var lastActivity = projectLogs.Select(x => x.CreatedAt)
                        .Concat(projectDocuments.Select(x => x.CreatedAt))
                        .DefaultIfEmpty(start)
                        .Max();

                    return new SolarAnalyticsProjectDto(
                        projectId,
                        projects.TryGetValue(projectId, out var name) ? name : $"Project #{projectId}",
                        projectLogs.Count,
                        projectDocuments.Count,
                        lastActivity);
                })
                .OrderByDescending(x => x.CalculationCount + x.DocumentCount)
                .ThenByDescending(x => x.LastActivityAt)
                .Take(10)
                .ToList();

            var recentLogActivities = logs
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .Select(x => new SolarAnalyticsRecentActivityDto(
                    "Calculation",
                    x.Source,
                    x.EventType,
                    x.SolarSalesProjectId.HasValue && projects.TryGetValue(x.SolarSalesProjectId.Value, out var name) ? name : null,
                    null,
                    null,
                    x.CreatedAt));

            var recentDocumentActivities = documents
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .Select(x => new SolarAnalyticsRecentActivityDto(
                    "Document",
                    SourceAdmin,
                    EventAdminDocx,
                    projects.TryGetValue(x.SolarSalesProjectId, out var name) ? name : null,
                    x.DocumentNumber,
                    x.DocumentCode,
                    x.CreatedAt));

            var dashboard = new SolarAnalyticsDashboardDto(
                new SolarAnalyticsSummaryDto(
                    logs.Count,
                    logs.Count(x => x.EventType == EventWebCalculation),
                    logs.Count(x => x.Source == SourceAdmin),
                    logs.Count(x => x.EventType == EventWebWhatsappClick),
                    documents.Count,
                    projectIds.Count),
                timeSeries,
                documents.GroupBy(x => x.DocumentCode)
                    .OrderByDescending(x => x.Count())
                    .Select(x => new SolarAnalyticsBreakdownDto(x.Key, x.Count()))
                    .ToList(),
                logs.GroupBy(x => x.Source)
                    .OrderByDescending(x => x.Count())
                    .Select(x => new SolarAnalyticsBreakdownDto(x.Key, x.Count()))
                    .ToList(),
                topProjects,
                recentLogActivities.Concat(recentDocumentActivities)
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(20)
                    .ToList());

            return ApiResponse<SolarAnalyticsDashboardDto>.SuccessResponse(dashboard);
        }

        private async Task<ApiResponse<SolarCalculationLogDto>> LogPublicEventAsync(PublicSolarTrackingRequest request, string eventType, CancellationToken ct)
        {
            try
            {
                var now = GetAzerbaijanNow();
                var log = CreateCalculationLog(
                    SourceWeb,
                    eventType,
                    null,
                    null,
                    request.Language,
                    request.SessionId,
                    ToPayloadJson(request.Payload),
                    now);

                _context.SolarCalculationLogs.Add(log);
                await _context.SaveChangesAsync(ct);

                return ApiResponse<SolarCalculationLogDto>.SuccessResponse(new SolarCalculationLogDto(0, log.Id));
            }
            catch
            {
                return ApiResponse<SolarCalculationLogDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while logging the public calculator event.");
            }
        }

        private async Task<SolarSalesProject> ResolveProjectAsync(string projectName, DateTime now, CancellationToken ct)
        {
            var trimmedName = projectName.Trim();
            var normalizedName = NormalizeName(trimmedName);
            var project = await _context.SolarSalesProjects.FirstOrDefaultAsync(x => x.NormalizedName == normalizedName, ct);

            if (project is null)
            {
                project = new SolarSalesProject
                {
                    Name = trimmedName,
                    NormalizedName = normalizedName,
                    CreatedAt = now,
                    IsActive = true
                };
                _context.SolarSalesProjects.Add(project);
                await _context.SaveChangesAsync(ct);
                return project;
            }

            if (!project.IsActive || project.Name != trimmedName)
            {
                project.Name = trimmedName;
                project.IsActive = true;
                project.UpdatedAt = now;
            }

            return project;
        }

        private static SolarCalculationLog CreateCalculationLog(
            string source,
            string eventType,
            int? projectId,
            int? adminUserId,
            string? language,
            string? sessionId,
            string payloadJson,
            DateTime now)
            => new()
            {
                Source = source,
                EventType = eventType,
                SolarSalesProjectId = projectId,
                AdminUserId = adminUserId,
                Language = TrimTo(language, 10),
                SessionId = TrimTo(sessionId, 100),
                PayloadJson = payloadJson,
                CreatedAt = now
            };

        private static string? ValidateProjectName(string? projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return "Project name is required.";
            }

            return projectName.Trim().Length > 200 ? "Project name must be 200 characters or fewer." : null;
        }

        private static string? ValidateDocumentCode(string? documentCode)
        {
            if (string.IsNullOrWhiteSpace(documentCode))
            {
                return "Document code is required.";
            }

            return AllowedDocumentCodes.Contains(documentCode.Trim()) ? null : "Document code is not supported.";
        }

        private static string NormalizeName(string value)
            => string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static string ToPayloadJson(JsonElement payload)
            => payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "{}" : payload.GetRawText();

        private static string TrimTo(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static DateTime GetAzerbaijanNow()
        {
            try
            {
                var timezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
            }
            catch
            {
                try
                {
                    var timezone = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
                }
                catch
                {
                    return DateTime.UtcNow.AddHours(4);
                }
            }
        }
    }
}
