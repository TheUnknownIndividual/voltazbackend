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
        private const string EventTrackerProjectAutoCreated = "TRACKER_PROJECT_AUTO_CREATED";
        private const string EventWebCalculation = "WEB_CALCULATION";
        private const string EventWebWhatsappClick = "WEB_WHATSAPP_CLICK";

        private static readonly HashSet<string> AllowedDocumentCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CP", "QUO", "INV", "PI", "CTR", "AGR", "PO", "SO", "BOQ", "PRJ", "DRW", "REP", "ACT", "LET", "MEM", "SPEC", "CAL"
        };

        private readonly DataContext _context;
        private readonly IAdminAuditService _audit;

        public SolarAnalyticsService(DataContext context, IAdminAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<ApiResponse<IReadOnlyList<SolarProjectDto>>> SearchProjectsAsync(string? query, CancellationToken ct = default)
        {
            var normalizedQuery = NormalizeName(query ?? string.Empty);
            var projectsQuery = _context.SolarSalesProjects.AsNoTracking().Where(x => x.IsActive);

            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                projectsQuery = projectsQuery.Where(x => x.NormalizedName.Contains(normalizedQuery));
            }

            var projectRows = await projectsQuery
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenBy(x => x.Name)
                .Take(12)
                .ToListAsync(ct);

            var projectIds = projectRows.Select(x => x.Id).ToList();
            var latestLogPayloads = await _context.SolarCalculationLogs
                .AsNoTracking()
                .Where(x => x.SolarSalesProjectId.HasValue && projectIds.Contains(x.SolarSalesProjectId.Value))
                .GroupBy(x => x.SolarSalesProjectId!.Value)
                .Select(g => new
                {
                    ProjectId = g.Key,
                    PayloadJson = g.OrderByDescending(x => x.CreatedAt).Select(x => x.PayloadJson).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.ProjectId, x => x.PayloadJson ?? string.Empty, ct);

            var latestDocumentPayloads = await _context.DocumentLogs
                .AsNoTracking()
                .Where(x => projectIds.Contains(x.SolarSalesProjectId))
                .GroupBy(x => x.SolarSalesProjectId)
                .Select(g => new
                {
                    ProjectId = g.Key,
                    PayloadJson = g.OrderByDescending(x => x.CreatedAt).Select(x => x.PayloadJson).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.ProjectId, x => x.PayloadJson ?? string.Empty, ct);

            var projects = projectRows
                .Select(x => new SolarProjectDto(
                    x.Id,
                    x.Name,
                    x.CreatedAt,
                    x.UpdatedAt,
                    latestDocumentPayloads.GetValueOrDefault(x.Id) ?? latestLogPayloads.GetValueOrDefault(x.Id) ?? string.Empty,
                    x.AdminTrackedProjectId))
                .ToList();

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
                var trackedProjectResolution = await ResolveTrackedProjectAsync(request.AdminTrackedProjectId, project, payloadJson, now, ct);
                var trackedProject = trackedProjectResolution.Project;
                LinkTrackerProject(project, trackedProject.Id, now);
                if (trackedProjectResolution.WasCreated)
                {
                    var trackerCreationLog = CreateCalculationLog(
                        SourceAdmin,
                        EventTrackerProjectAutoCreated,
                        project.Id,
                        adminUserId,
                        request.Language,
                        request.SessionId,
                        JsonSerializer.Serialize(new
                        {
                            trackerProjectId = trackedProject.Id,
                            trackerProjectName = trackedProject.Name,
                            createdAt = now
                        }),
                        now);
                    trackerCreationLog.AdminTrackedProjectId = trackedProject.Id;
                    _context.SolarCalculationLogs.Add(trackerCreationLog);
                }
                var calculationLog = CreateCalculationLog(
                    SourceAdmin,
                    EventAdminDocx,
                    project.Id,
                    adminUserId,
                    request.Language,
                    request.SessionId,
                    payloadJson,
                    now);
                calculationLog.AdminTrackedProjectId = trackedProject.Id;

                _context.SolarCalculationLogs.Add(calculationLog);
                await _context.SaveChangesAsync(ct);

                var projectDocuments = await _context.DocumentLogs
                    .Where(x => x.SolarSalesProjectId == project.Id)
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .ToListAsync(ct);
                var projectSequenceNumbers = projectDocuments
                    .Select(x => ExtractDocumentSequence(x.DocumentNumber))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToList();
                int? existingProjectSequenceNumber = projectSequenceNumbers.Count > 0
                    ? projectSequenceNumbers.Min()
                    : null;

                if (existingProjectSequenceNumber.HasValue)
                {
                    foreach (var duplicate in projectDocuments.Where(x =>
                        ExtractDocumentSequence(x.DocumentNumber) is int sequence && sequence != existingProjectSequenceNumber.Value))
                    {
                        _context.DocumentLogs.Remove(duplicate);
                    }
                }

                var sequence = await _context.DocumentSequences
                    .FirstOrDefaultAsync(x => x.DocumentCode == documentCode && x.Year == now.Year && x.Month == now.Month, ct);
                var highestIssuedSequenceNumber = await GetHighestIssuedSequenceNumberAsync(ct);
                int sequenceNumber;

                if (existingProjectSequenceNumber.HasValue)
                {
                    sequenceNumber = existingProjectSequenceNumber.Value;

                    if (sequence is null)
                    {
                        sequence = new DocumentSequence
                        {
                            DocumentCode = documentCode,
                            Year = now.Year,
                            Month = now.Month,
                            CurrentNumber = Math.Max(highestIssuedSequenceNumber, sequenceNumber),
                            CreatedAt = now
                        };
                        _context.DocumentSequences.Add(sequence);
                    }
                    else
                    {
                        sequence.CurrentNumber = Math.Max(sequence.CurrentNumber, sequenceNumber);
                        sequence.UpdatedAt = now;
                    }
                }
                else
                {
                    sequenceNumber = Math.Max(highestIssuedSequenceNumber, FirstDocumentNumber - 1) + 1;

                    if (sequence is null)
                    {
                        sequence = new DocumentSequence
                        {
                            DocumentCode = documentCode,
                            Year = now.Year,
                            Month = now.Month,
                            CurrentNumber = sequenceNumber,
                            CreatedAt = now
                        };
                        _context.DocumentSequences.Add(sequence);
                    }
                    else
                    {
                        sequence.CurrentNumber = sequenceNumber;
                        sequence.UpdatedAt = now;
                    }
                }

                var documentNumber = $"VOLT-{documentCode}-{now:yyyy-MM}-{sequenceNumber:D3}";
                var documentLog = await _context.DocumentLogs
                    .FirstOrDefaultAsync(x => x.SolarSalesProjectId == project.Id && x.DocumentNumber == documentNumber, ct);

                if (documentLog is null)
                {
                    documentLog = new DocumentLog
                    {
                        DocumentCode = documentCode,
                        DocumentNumber = documentNumber,
                        SolarSalesProjectId = project.Id,
                        SolarCalculationLogId = calculationLog.Id,
                        AdminUserId = adminUserId,
                        AdminTrackedProjectId = trackedProject.Id,
                        PayloadJson = payloadJson,
                        CreatedAt = now
                    };

                    _context.DocumentLogs.Add(documentLog);
                }
                else
                {
                    documentLog.DocumentCode = documentCode;
                    documentLog.SolarCalculationLogId = calculationLog.Id;
                    documentLog.AdminUserId = adminUserId;
                    documentLog.AdminTrackedProjectId = trackedProject.Id;
                    documentLog.PayloadJson = payloadJson;
                    documentLog.CreatedAt = now;
                }

                var issuer = adminUserId.HasValue
                    ? await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == adminUserId.Value, ct)
                    : null;
                var verification = documentLog.Id > 0
                    ? await _context.DocumentVerifications.FirstOrDefaultAsync(x => x.DocumentLogId == documentLog.Id, ct)
                    : null;
                if (verification is null)
                {
                    verification = new DocumentVerification { DocumentLog = documentLog };
                    _context.DocumentVerifications.Add(verification);
                }
                DocumentVerificationService.Populate(
                    verification,
                    documentLog,
                    DocumentVerificationService.GetIssuerDisplayName(issuer),
                    now);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                try
                {
                    await _audit.WriteAsync(adminUserId, issuer?.Username, "DOCUMENT_ISSUED", "Document", documentLog.Id.ToString(), documentNumber, true, ct);
                }
                catch
                {
                    // The issued document and its verification record were already committed.
                    // Logging must never turn a successful issuance into a false failure.
                }

                return ApiResponse<SolarDocumentIssueDto>.SuccessResponse(new SolarDocumentIssueDto(
                    project.Id,
                    trackedProject.Id,
                    calculationLog.Id,
                    documentLog.Id,
                    documentCode,
                    documentNumber,
                    verification.PublicToken,
                    DocumentVerificationService.GetPublicUrl(verification.PublicToken)));
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

            var trackedProject = await _context.AdminTrackedProjects.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.AdminTrackedProjectId && x.IsActive, ct);
            if (trackedProject is null)
            {
                return ApiResponse<SolarCalculationLogDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "An active tracker project must be selected before exporting a presentation.");
            }

            try
            {
                var now = GetAzerbaijanNow();
                var project = await ResolveProjectAsync(request.ProjectName, now, ct);
                LinkTrackerProject(project, trackedProject.Id, now);
                var calculationLog = CreateCalculationLog(
                    SourceAdmin,
                    EventAdminPdf,
                    project.Id,
                    adminUserId,
                    request.Language,
                    request.SessionId,
                    ToPayloadJson(request.Payload),
                    now);
                calculationLog.AdminTrackedProjectId = trackedProject.Id;

                _context.SolarCalculationLogs.Add(calculationLog);
                await _context.SaveChangesAsync(ct);
                await _audit.WriteAsync(adminUserId, null, "PDF_EXPORTED", "SolarProject", project.Id.ToString(), project.Name, true, ct);

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
            var calculationLogs = logs.Where(x => x.EventType != EventTrackerProjectAutoCreated).ToList();
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
                    calculationLogs.Count(x => x.CreatedAt >= day && x.CreatedAt < next && x.EventType != EventWebWhatsappClick),
                    documents.Count(x => x.CreatedAt >= day && x.CreatedAt < next),
                    logs.Count(x => x.CreatedAt >= day && x.CreatedAt < next && x.EventType == EventWebWhatsappClick));
            }).ToList();

            var projectIds = calculationLogs.Select(x => x.SolarSalesProjectId)
                .Concat(documents.Select(x => (int?)x.SolarSalesProjectId))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var topProjects = projectIds
                .Select(projectId =>
                {
                    var projectLogs = calculationLogs.Where(x => x.SolarSalesProjectId == projectId).ToList();
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
                    x.CreatedAt,
                    x.PayloadJson));

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
                    x.CreatedAt,
                    x.PayloadJson));

            var dashboard = new SolarAnalyticsDashboardDto(
                new SolarAnalyticsSummaryDto(
                    calculationLogs.Count,
                    calculationLogs.Count(x => x.EventType == EventWebCalculation),
                    calculationLogs.Count(x => x.Source == SourceAdmin),
                    calculationLogs.Count(x => x.EventType == EventWebWhatsappClick),
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

        private async Task<TrackedProjectResolution> ResolveTrackedProjectAsync(int requestedProjectId, SolarSalesProject salesProject, string payloadJson, DateTime now, CancellationToken ct)
        {
            if (requestedProjectId > 0)
            {
                var selectedProject = await _context.AdminTrackedProjects
                    .FirstOrDefaultAsync(x => x.Id == requestedProjectId && x.IsActive, ct);
                if (selectedProject is not null)
                {
                    return new TrackedProjectResolution(selectedProject, false);
                }
            }

            if (salesProject.AdminTrackedProjectId.HasValue)
            {
                var linkedProject = await _context.AdminTrackedProjects
                    .FirstOrDefaultAsync(x => x.Id == salesProject.AdminTrackedProjectId.Value && x.IsActive, ct);
                if (linkedProject is not null)
                {
                    return new TrackedProjectResolution(linkedProject, false);
                }
            }

            var normalizedName = salesProject.Name.Trim().ToUpperInvariant();
            var matchingProject = await _context.AdminTrackedProjects
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(x => x.Name.ToUpper() == normalizedName, ct);
            if (matchingProject is not null)
            {
                // Earlier automatic exports could leave a minimal tracker record behind.
                // Complete only an otherwise-empty matching record; never overwrite a
                // project an admin has already described.
                if (string.IsNullOrWhiteSpace(matchingProject.Description))
                {
                    var existingDraft = ReadAutoTrackedProjectDraft(payloadJson);
                    matchingProject.Location = existingDraft.Address;
                    matchingProject.PersonName = existingDraft.Recipient;
                    matchingProject.SystemType = existingDraft.SystemType;
                    matchingProject.OfferSentAt = now;
                    matchingProject.CurrentStatus = "Sorgu Gelib";
                    matchingProject.SmallNote = $"{existingDraft.SystemTypeLabel} · {existingDraft.SystemKw:0.##} kW · İlkin qiymətləndirmə sənədi";
                    matchingProject.OfferPrice = existingDraft.TotalPriceAzn;
                    matchingProject.IncludesAdv = existingDraft.IncludesAdv;
                    matchingProject.Description = BuildAutoTrackedProjectDescription(existingDraft);
                    matchingProject.UpdatedAt = now;

                    var hasOffer = await _context.AdminTrackedProjectOffers
                        .AnyAsync(x => x.AdminTrackedProjectId == matchingProject.Id && x.IsActive, ct);
                    if (!hasOffer)
                    {
                        _context.AdminTrackedProjectOffers.Add(new AdminTrackedProjectOffer
                        {
                            AdminTrackedProjectId = matchingProject.Id,
                            Power = existingDraft.SystemKw,
                            MountType = existingDraft.MountType,
                            AreaType = $"{existingDraft.SystemTypeLabel} · {existingDraft.PanelWattage:0}W panel",
                            ExtraAmount = 0,
                            SentAt = now,
                            IsActive = true
                        });
                    }

                    await _context.SaveChangesAsync(ct);
                }

                return new TrackedProjectResolution(matchingProject, false);
            }

            var draft = ReadAutoTrackedProjectDraft(payloadJson);
            var project = new AdminTrackedProject
            {
                Name = salesProject.Name,
                Location = draft.Address,
                PersonName = draft.Recipient,
                SystemType = draft.SystemType,
                OfferSentAt = now,
                CurrentStatus = "Sorgu Gelib",
                SmallNote = $"{draft.SystemTypeLabel} · {draft.SystemKw:0.##} kW · İlkin qiymətləndirmə sənədi",
                OfferPrice = draft.TotalPriceAzn,
                IncludesAdv = draft.IncludesAdv,
                Description = BuildAutoTrackedProjectDescription(draft),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            project.Offers.Add(new AdminTrackedProjectOffer
            {
                Power = draft.SystemKw,
                MountType = draft.MountType,
                AreaType = $"{draft.SystemTypeLabel} · {draft.PanelWattage:0}W panel",
                ExtraAmount = 0,
                SentAt = now,
                IsActive = true
            });
            _context.AdminTrackedProjects.Add(project);
            await _context.SaveChangesAsync(ct);
            return new TrackedProjectResolution(project, true);
        }

        private sealed record TrackedProjectResolution(AdminTrackedProject Project, bool WasCreated);

        private sealed record AutoTrackedProjectDraft(
            string Address,
            string Recipient,
            byte SystemType,
            string SystemTypeLabel,
            decimal SystemKw,
            decimal PanelWattage,
            decimal AnnualSavingsAzn,
            decimal TotalPriceAzn,
            string InstallationDays,
            string InverterModel,
            string InverterCount,
            string MountType,
            bool IncludesAdv);

        private static AutoTrackedProjectDraft ReadAutoTrackedProjectDraft(string payloadJson)
        {
            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                var root = document.RootElement;
                var inputs = root.TryGetProperty("inputs", out var inputsElement) ? inputsElement : default;
                var result = root.TryGetProperty("result", out var resultElement) ? resultElement : default;
                var systemType = ReadString(inputs, "systemType").ToLowerInvariant();
                var mappedSystemType = systemType == "off-grid" ? (byte)2 : systemType == "hybrid" ? (byte)3 : (byte)1;
                var systemTypeLabel = mappedSystemType == 2 ? "Off-Grid" : mappedSystemType == 3 ? "Hybrid" : "On-Grid";
                var mountType = ReadString(inputs, "mountType").Equals("ground", StringComparison.OrdinalIgnoreCase) ? "ground" : "roof";
                var inverterModel = TrimTo(ReadString(inputs, "inverterModel"), 300) ?? string.Empty;
                var inverterCount = TrimTo(ReadString(inputs, "inverterCount"), 40) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(inverterModel))
                {
                    inverterModel = TrimTo(ReadString(result, "inverter"), 300) ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(inverterCount))
                {
                    var calculatedInverterCount = ReadDecimal(result, "inverterQuantity");
                    inverterCount = calculatedInverterCount > 0 ? calculatedInverterCount.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
                }

                return new AutoTrackedProjectDraft(
                    TrimTo(ReadString(inputs, "address"), 300) ?? string.Empty,
                    TrimTo(ReadString(inputs, "recipient"), 160) ?? string.Empty,
                    mappedSystemType,
                    systemTypeLabel,
                    Math.Max(0, ReadDecimal(result, "systemKw")),
                    Math.Max(0, ReadDecimal(inputs, "panelWattage")),
                    Math.Max(0, ReadDecimal(result, "annualSavings")),
                    Math.Max(0, ReadDecimal(result, "totalPriceAzn")),
                    TrimTo(ReadString(inputs, "installationDays"), 40) ?? string.Empty,
                    inverterModel,
                    inverterCount,
                    mountType,
                    ReadBoolean(inputs, "includesAdv", true));
            }
            catch
            {
                return new AutoTrackedProjectDraft(string.Empty, string.Empty, 1, "On-Grid", 0, 0, 0, 0, string.Empty, string.Empty, string.Empty, "roof", true);
            }
        }

        private static string BuildAutoTrackedProjectDescription(AutoTrackedProjectDraft draft)
            => string.Join(Environment.NewLine, new[]
            {
                "İlkin qiymətləndirmə sənədi ilə avtomatik yaradılıb.",
                $"Sistem növü: {draft.SystemTypeLabel}",
                $"Ünvan: {draft.Address}",
                $"Kimə: {draft.Recipient}",
                $"Panel gücü: {draft.PanelWattage:0} W",
                $"İllik qənaət: {draft.AnnualSavingsAzn:0.##} AZN",
                $"Quraşdırma müddəti: {draft.InstallationDays} gün",
                $"İnverter: {draft.InverterCount} × {draft.InverterModel}",
                $"ƏDV: {(draft.IncludesAdv ? "daxildir" : "daxil deyil")}"
            }.Where(line => !string.IsNullOrWhiteSpace(line)));

        private static string ReadString(JsonElement element, string propertyName)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static decimal ReadDecimal(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            {
                return 0;
            }

            return property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number)
                ? number
                : property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;
        }

        private static bool ReadBoolean(JsonElement element, string propertyName, bool fallback)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : fallback;

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

        private static void LinkTrackerProject(SolarSalesProject project, int adminTrackedProjectId, DateTime now)
        {
            if (project.AdminTrackedProjectId == adminTrackedProjectId)
            {
                return;
            }

            project.AdminTrackedProjectId = adminTrackedProjectId;
            project.UpdatedAt = now;
        }

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

        private async Task<int> GetHighestIssuedSequenceNumberAsync(CancellationToken ct)
        {
            var sequenceNumbers = await _context.DocumentSequences
                .Select(x => x.CurrentNumber)
                .ToListAsync(ct);
            var documentNumbers = await _context.DocumentLogs
                .Select(x => x.DocumentNumber)
                .ToListAsync(ct);

            return sequenceNumbers
                .Concat(documentNumbers.Select(ExtractDocumentSequence).Where(x => x.HasValue).Select(x => x!.Value))
                .DefaultIfEmpty(FirstDocumentNumber - 1)
                .Max();
        }

        private static int? ExtractDocumentSequence(string? documentNumber)
        {
            if (string.IsNullOrWhiteSpace(documentNumber))
            {
                return null;
            }

            var parts = documentNumber.Split('-');
            return parts.Length == 5 && int.TryParse(parts[^1], out var sequenceNumber)
                ? sequenceNumber
                : null;
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
