using Volt.Application.Dtos;
using Volt.Application.Dtos.AdminProjectTracker;
using Volt.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class AdminProjectTrackerService : IAdminProjectTrackerService
    {
        private static readonly string[] AllowedMountTypes = ["roof", "ground"];
        private const decimal VatRate = 0.18m;
        private readonly IUnitOfWork _uow;
        private readonly ITelegramTaskNotificationService _telegram;
        private readonly TelegramBotOptions _telegramOptions;
        private readonly ILogger<AdminProjectTrackerService> _logger;

        public AdminProjectTrackerService(IUnitOfWork uow, ITelegramTaskNotificationService telegram, IOptions<TelegramBotOptions> telegramOptions, ILogger<AdminProjectTrackerService> logger)
        {
            _uow = uow;
            _telegram = telegram;
            _telegramOptions = telegramOptions.Value;
            _logger = logger;
        }

        public async Task<ApiResponse<IReadOnlyList<AdminTrackedProjectDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var projects = await _uow.Repository<AdminTrackedProject>().ListNoTrackingAsync(x => x.IsActive, ct);
            var result = new List<AdminTrackedProjectDto>(projects.Count);

            foreach (var project in projects.OrderByDescending(x => x.CreatedAt))
            {
                result.Add(await MapToDtoAsync(project, ct));
            }

            return ApiResponse<IReadOnlyList<AdminTrackedProjectDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<AdminTrackedProjectDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var project = await _uow.Repository<AdminTrackedProject>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (project is null)
            {
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND, ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND);
            }

            return ApiResponse<AdminTrackedProjectDto>.SuccessResponse(await MapToDtoAsync(project, ct));
        }

        public async Task<ApiResponse<AdminTrackedProjectDto>> CreateAsync(AdminTrackedProjectUpsertRequest request, int actorAdminUserId, CancellationToken ct = default)
        {
            var validationError = ValidateRequest(request);
            if (validationError is not null)
            {
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.INVALID_ADMIN_TRACKED_PROJECT_REQUEST, validationError);
            }

            var now = DateTime.UtcNow;
            var project = new AdminTrackedProject
            {
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true
            };

            ApplyRequest(project, request, now);
            project.Offers = BuildOffers(request);
            project.Attachments = BuildAttachments(request);

            await _uow.Repository<AdminTrackedProject>().AddAsync(project, ct);
            await _uow.SaveChangesAsync(ct);

            if (IsAccepted(project.CurrentStatus))
                await RequestStakeholderApprovalAsync(project, actorAdminUserId, ct);

            return ApiResponse<AdminTrackedProjectDto>.SuccessResponse(await MapToDtoAsync(project, ct));
        }

        public async Task<ApiResponse<AdminTrackedProjectDto>> UpdateAsync(int id, AdminTrackedProjectUpsertRequest request, int actorAdminUserId, CancellationToken ct = default)
        {
            var validationError = ValidateRequest(request);
            if (validationError is not null)
            {
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.INVALID_ADMIN_TRACKED_PROJECT_REQUEST, validationError);
            }

            var project = await _uow.Repository<AdminTrackedProject>()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (project is null)
            {
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND, ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND);
            }

            if (project.StakeholderApprovalStatus == "Declined" && !await IsSuperAdminAsync(actorAdminUserId, ct))
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This project is ON HOLD. Only a full admin can edit it; other users can request a new stakeholder review.");

            var existingOffers = await _uow.Repository<AdminTrackedProjectOffer>()
                .ListNoTrackingAsync(x => x.AdminTrackedProjectId == id, ct);
            foreach (var offer in existingOffers)
            {
                _uow.Repository<AdminTrackedProjectOffer>().Remove(offer);
            }

            var existingAttachments = await _uow.Repository<AdminTrackedProjectAttachment>()
                .ListNoTrackingAsync(x => x.AdminTrackedProjectId == id, ct);
            foreach (var attachment in existingAttachments)
            {
                _uow.Repository<AdminTrackedProjectAttachment>().Remove(attachment);
            }

            var wasAccepted = IsAccepted(project.CurrentStatus);
            var willBeAccepted = IsAccepted(request.CurrentStatus);
            var now = DateTime.UtcNow;
            ApplyRequest(project, request, now);
            _uow.Repository<AdminTrackedProject>().Update(project);

            foreach (var offer in BuildOffers(request))
            {
                offer.AdminTrackedProjectId = id;
                await _uow.Repository<AdminTrackedProjectOffer>().AddAsync(offer, ct);
            }

            foreach (var attachment in BuildAttachments(request))
            {
                attachment.AdminTrackedProjectId = id;
                await _uow.Repository<AdminTrackedProjectAttachment>().AddAsync(attachment, ct);
            }

            await _uow.SaveChangesAsync(ct);

            if (!wasAccepted && willBeAccepted)
                await RequestStakeholderApprovalAsync(project, actorAdminUserId, ct);
            else if (wasAccepted && !willBeAccepted)
                await CancelStakeholderApprovalAsync(project, "Project status changed away from Qebul Edildi", ct);

            return await GetByIdAsync(project.Id, ct);
        }

        public async Task<ApiResponse<AdminTrackedProjectDto>> RequestStakeholderReviewAsync(int id, int actorAdminUserId, CancellationToken ct = default)
        {
            var project = await _uow.Repository<AdminTrackedProject>().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (project is null) return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND, ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND);
            if (project.StakeholderApprovalStatus != "Declined") return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "A new review can be requested only for an ON HOLD project.");
            await RequestStakeholderApprovalAsync(project, actorAdminUserId, ct);
            return await GetByIdAsync(project.Id, ct);
        }

        public async Task<ApiResponse<AdminTrackedProjectDto>> RetryStakeholderApprovalAsync(int id, int actorAdminUserId, CancellationToken ct = default)
        {
            if (!await IsSuperAdminAsync(actorAdminUserId, ct))
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Only a full admin can retry a failed stakeholder notification.");
            var project = await _uow.Repository<AdminTrackedProject>().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (project is null) return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND, ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND);
            if (!IsAccepted(project.CurrentStatus) || project.StakeholderApprovalStatus != "DeliveryFailed")
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Only a failed approval notification for an accepted project can be retried.");
            await RequestStakeholderApprovalAsync(project, actorAdminUserId, ct);
            return await GetByIdAsync(id, ct);
        }

        public async Task<ApiResponse<AdminTrackedProjectDto>> RecordStakeholderDecisionAsync(int requestId, long telegramChatId, bool approved, CancellationToken ct = default)
        {
            var stakeholder = await _uow.Repository<AdminUser>().FirstOrDefaultNoTrackingAsync(x => x.IsActive && x.IsStakeholder && x.TelegramChatId == telegramChatId, ct);
            if (stakeholder is null) return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This Telegram account is not an active stakeholder.");
            var request = await _uow.Repository<StakeholderApprovalRequest>().FirstOrDefaultAsync(x => x.Id == requestId, ct);
            if (request is null) return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This stakeholder request no longer exists.");
            if (!string.Equals(request.EnvironmentScope, CurrentEnvironmentScope(), StringComparison.Ordinal))
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This stakeholder request belongs to a different Volt environment.");
            var recipient = await _uow.Repository<StakeholderApprovalRecipient>().FirstOrDefaultNoTrackingAsync(x => x.StakeholderApprovalRequestId == requestId && x.AdminUserId == stakeholder.Id && x.TelegramChatId == telegramChatId && x.DeliveryStatus == "Delivered", ct);
            if (recipient is null) return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This Telegram account did not receive this stakeholder request.");
            var project = await _uow.Repository<AdminTrackedProject>().FirstOrDefaultAsync(x => x.Id == request.AdminTrackedProjectId && x.IsActive, ct);
            if (project is null) return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND, ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND);
            if (request.Status != "Pending" || !IsAccepted(project.CurrentStatus)) return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This stakeholder request has already been resolved or cancelled.");
            var now = DateTime.UtcNow;
            request.Status = approved ? "Approved" : "Declined";
            request.ResolvedAt = now;
            request.ResolvedByAdminUserId = stakeholder.Id;
            SetVisibleApprovalState(project, request.Status, now, stakeholder.Id);
            _uow.Repository<StakeholderApprovalRequest>().Update(request);
            _uow.Repository<AdminTrackedProject>().Update(project);
            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This stakeholder request has already been resolved.");
            }
            await CreateOrUpdateExecutionAsync(project, stakeholder.Id, approved ? "Aktiv" : "ON HOLD", ct);
            return await GetByIdAsync(project.Id, ct);
        }

        private async Task RequestStakeholderApprovalAsync(AdminTrackedProject project, int actorAdminUserId, CancellationToken ct)
        {
            var stakeholders = await _uow.Repository<AdminUser>().ListNoTrackingAsync(x => x.IsActive && x.IsStakeholder && x.TelegramChatId.HasValue, ct);
            var offers = await _uow.Repository<AdminTrackedProjectOffer>().ListNoTrackingAsync(x => x.AdminTrackedProjectId == project.Id && x.IsActive, ct);
            var attachments = await _uow.Repository<AdminTrackedProjectAttachment>().ListNoTrackingAsync(x => x.AdminTrackedProjectId == project.Id && x.IsActive, ct);
            var actor = await _uow.Repository<AdminUser>().FirstOrDefaultNoTrackingAsync(x => x.Id == actorAdminUserId, ct);
            var notification = BuildStakeholderNotification(project, offers, attachments, DisplayName(actor, actorAdminUserId));
            var now = DateTime.UtcNow;
            var request = new StakeholderApprovalRequest
            {
                AdminTrackedProjectId = project.Id,
                EnvironmentScope = CurrentEnvironmentScope(),
                Status = "Dispatching",
                CreatedAt = now,
                Recipients = stakeholders.GroupBy(x => x.TelegramChatId!.Value).Select(group => new StakeholderApprovalRecipient
                {
                    AdminUserId = group.First().Id,
                    TelegramChatId = group.Key,
                    DeliveryStatus = "Dispatching"
                }).ToList()
            };
            SetVisibleApprovalState(project, "Dispatching", now, null);
            await _uow.Repository<StakeholderApprovalRequest>().AddAsync(request, ct);
            _uow.Repository<AdminTrackedProject>().Update(project);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Stakeholder approval dispatch started. ProjectId={ProjectId} RequestId={RequestId} Environment={Environment} CandidateCount={CandidateCount} BotConfigured={BotConfigured}",
                project.Id, request.Id, request.EnvironmentScope, request.Recipients.Count, !string.IsNullOrWhiteSpace(_telegramOptions.BotToken));

            foreach (var recipient in request.Recipients)
            {
                var stakeholder = stakeholders.First(x => x.Id == recipient.AdminUserId);
                var outcome = await _telegram.SendProjectApprovalRequestAsync(recipient.TelegramChatId, request.Id, request.EnvironmentScope, notification, UsesRussianStakeholderMessage(stakeholder), ct);
                _logger.LogInformation(
                    "Stakeholder approval delivery completed. ProjectId={ProjectId} RequestId={RequestId} ChatId={ChatId} Outcome={Outcome}",
                    project.Id, request.Id, MaskChatId(recipient.TelegramChatId), outcome);
                recipient.DeliveryStatus = outcome == "Delivered" ? "Delivered" : "Failed";
                recipient.DeliveredAt = outcome == "Delivered" ? DateTime.UtcNow : null;
                recipient.FailureStatus = outcome == "Delivered" ? string.Empty : outcome;
                _uow.Repository<StakeholderApprovalRecipient>().Update(recipient);
            }

            var delivered = request.Recipients.Any(x => x.DeliveryStatus == "Delivered");
            request.Status = delivered ? "Pending" : "DeliveryFailed";
            request.DispatchCompletedAt = DateTime.UtcNow;
            SetVisibleApprovalState(project, request.Status, request.CreatedAt, null);
            _uow.Repository<StakeholderApprovalRequest>().Update(request);
            _uow.Repository<AdminTrackedProject>().Update(project);
            await _uow.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Stakeholder approval dispatch finished. ProjectId={ProjectId} RequestId={RequestId} Status={Status} Delivered={Delivered} Failed={Failed}",
                project.Id, request.Id, request.Status, request.Recipients.Count(x => x.DeliveryStatus == "Delivered"), request.Recipients.Count(x => x.DeliveryStatus == "Failed"));
        }

        private static string MaskChatId(long chatId)
        {
            var value = chatId.ToString();
            return value.Length <= 4 ? "****" : $"{new string('*', value.Length - 4)}{value[^4..]}";
        }

        private async Task CancelStakeholderApprovalAsync(AdminTrackedProject project, string reason, CancellationToken ct)
        {
            var unresolved = await _uow.Repository<StakeholderApprovalRequest>().ListNoTrackingAsync(x => x.AdminTrackedProjectId == project.Id && (x.Status == "Dispatching" || x.Status == "Pending" || x.Status == "DeliveryFailed"), ct);
            var now = DateTime.UtcNow;
            foreach (var item in unresolved)
            {
                item.Status = "Cancelled";
                item.CancelledAt = now;
                item.CancellationReason = reason;
                _uow.Repository<StakeholderApprovalRequest>().Update(item);
            }
            SetVisibleApprovalState(project, "NotRequired", null, null);
            _uow.Repository<AdminTrackedProject>().Update(project);
            await ArchiveExecutionAsync(project.Id, reason, ct);
            await _uow.SaveChangesAsync(ct);
        }

        private async Task ArchiveExecutionAsync(int trackedProjectId, string reason, CancellationToken ct)
        {
            var execution = await _uow.Repository<ExecutionProject>().FirstOrDefaultAsync(x => x.AdminTrackedProjectId == trackedProjectId && !x.ArchivedAt.HasValue, ct);
            if (execution is null) return;
            execution.ArchivedAt = DateTime.UtcNow;
            execution.ArchiveReason = reason;
            execution.UpdatedAt = execution.ArchivedAt;
            _uow.Repository<ExecutionProject>().Update(execution);
        }

        private static void SetVisibleApprovalState(AdminTrackedProject project, string status, DateTime? requestedAt, int? resolvedByAdminUserId)
        {
            project.StakeholderApprovalStatus = status;
            project.StakeholderApprovalRequestedAt = requestedAt;
            project.StakeholderApprovalResolvedAt = status is "Approved" or "Declined" ? DateTime.UtcNow : null;
            project.StakeholderApprovalResolvedByAdminUserId = resolvedByAdminUserId;
            project.UpdatedAt = DateTime.UtcNow;
        }

        private string CurrentEnvironmentScope() => string.Equals(_telegramOptions.ConnectionLinkScope, "test", StringComparison.OrdinalIgnoreCase) ? "test" : "prod";

        private static StakeholderApprovalNotification BuildStakeholderNotification(AdminTrackedProject project, IEnumerable<AdminTrackedProjectOffer> offers, IEnumerable<AdminTrackedProjectAttachment> attachments, string sentBy) => new()
        {
            SentBy = sentBy,
            ProjectName = project.Name,
            Location = project.Location,
            ContactName = project.PersonName,
            PhoneNumber = project.PhoneNumber,
            CurrentStatus = project.CurrentStatus,
            OfferPrice = project.OfferPrice,
            ProjectDate = project.ProjectDate,
            ResponseExpectedAt = project.ResponseExpectedAt,
            SmallNote = project.SmallNote,
            Description = project.Description,
            Offers = offers.OrderBy(x => x.Id).Select(x => new StakeholderApprovalOfferNotification(x.Power, x.MountType, x.AreaType, x.ExtraAmount)).ToList(),
            Attachments = attachments.OrderBy(x => x.Id).Select(x => new StakeholderApprovalAttachmentNotification(x.FileName, x.FilePath, x.Label)).ToList()
        };

        private static bool UsesRussianStakeholderMessage(AdminUser user)
        {
            var name = NormalizeStakeholderName(string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName);
            return name is "fakhri alakbarov" or "fexri elekberov" or "talat akhundov";
        }

        private static string NormalizeStakeholderName(string value)
            => (value ?? string.Empty).Trim().ToLowerInvariant().Replace('ə', 'e').Replace('ı', 'i').Replace("  ", " ");

        private static string DisplayName(AdminUser? user, int fallbackId)
            => user is null ? $"Admin #{fallbackId}" : (string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName);

        private async Task CreateOrUpdateExecutionAsync(AdminTrackedProject project, int managerId, string status, CancellationToken ct)
        {
            var existing = await _uow.Repository<ExecutionProject>().FirstOrDefaultAsync(x => x.AdminTrackedProjectId == project.Id, ct);
            if (existing is not null)
            {
                existing.Status = status;
                existing.ArchivedAt = null;
                existing.ArchiveReason = string.Empty;
                existing.UpdatedAt = DateTime.UtcNow;
                _uow.Repository<ExecutionProject>().Update(existing);
                await PrefillTrackedExecutionBoqAsync(existing, project, ct);
                await _uow.SaveChangesAsync(ct);
                return;
            }
            var manager = await _uow.Repository<AdminUser>().FirstOrDefaultNoTrackingAsync(x => x.Id == managerId && x.IsActive, ct);
            if (manager is null)
                return;

            var stakeholders = await _uow.Repository<AdminUser>().ListNoTrackingAsync(x => x.IsActive && x.IsStakeholder, ct);
            var executionProject = new ExecutionProject
            {
                AdminTrackedProjectId = project.Id,
                ProjectManagerAdminUserId = manager.Id,
                Status = status,
                PlannedStartDate = project.ProjectDate?.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _uow.Repository<ExecutionProject>().AddAsync(executionProject, ct);
            await _uow.SaveChangesAsync(ct);

            await PrefillTrackedExecutionBoqAsync(executionProject, project, ct);

            foreach (var stakeholder in stakeholders.Where(x => x.Id != manager.Id))
            {
                await _uow.Repository<ExecutionProjectStaff>().AddAsync(new ExecutionProjectStaff
                {
                    ExecutionProjectId = executionProject.Id,
                    AdminUserId = stakeholder.Id,
                    RoleName = "Stakeholder",
                    AddedAt = DateTime.UtcNow
                }, ct);
            }
            await _uow.SaveChangesAsync(ct);

        }

        private async Task PrefillTrackedExecutionBoqAsync(ExecutionProject executionProject, AdminTrackedProject project, CancellationToken ct)
        {
            if (await _uow.Repository<ExecutionProjectBoqItem>().AnyAsync(x => x.ExecutionProjectId == executionProject.Id, ct)) return;
            var offers = await _uow.Repository<AdminTrackedProjectOffer>().ListNoTrackingAsync(x => x.AdminTrackedProjectId == project.Id && x.IsActive, ct);
            foreach (var offer in offers.OrderBy(x => x.Id))
            {
                await _uow.Repository<ExecutionProjectBoqItem>().AddAsync(new ExecutionProjectBoqItem
                {
                    ExecutionProjectId = executionProject.Id,
                    ItemName = $"Mənbə təklifi #{offer.Id}" + (string.IsNullOrWhiteSpace(offer.AreaType) ? string.Empty : $" · {offer.AreaType}"),
                    Unit = "kW",
                    PlannedQuantity = offer.Power,
                    UnitCost = offer.Power > 0 && offer.ExtraAmount > 0 ? offer.ExtraAmount / offer.Power : null,
                    Note = $"Layihə təklifindən ilkin BOQ sətri; montaj: {offer.MountType}. Məhsulu icra zamanı seçin.",
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
        }

        private async Task<bool> IsSuperAdminAsync(int adminUserId, CancellationToken ct) => (await _uow.Repository<AdminUser>().FirstOrDefaultNoTrackingAsync(x => x.Id == adminUserId && x.IsActive, ct))?.IsSuperAdmin == true;

        public async Task<ApiResponse<AdminTrackedProjectDto>> AddAttachmentAsync(int id, AdminTrackedProjectAttachmentRequest request, CancellationToken ct = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.FilePath))
            {
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.INVALID_ADMIN_TRACKED_PROJECT_REQUEST, "Attachment file path is required.");
            }

            var project = await _uow.Repository<AdminTrackedProject>()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (project is null)
            {
                return ApiResponse<AdminTrackedProjectDto>.ErrorResponse(ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND, ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND);
            }

            await _uow.Repository<AdminTrackedProjectAttachment>().AddAsync(new AdminTrackedProjectAttachment
            {
                AdminTrackedProjectId = id,
                FileName = string.IsNullOrWhiteSpace(request.FileName) ? "Document.docx" : Normalize(request.FileName, 260),
                FilePath = request.FilePath.Trim(),
                Label = Normalize(request.Label, 120),
                IsActive = true
            }, ct);
            project.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<AdminTrackedProject>().Update(project);
            await _uow.SaveChangesAsync(ct);

            return await GetByIdAsync(id, ct);
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var project = await _uow.Repository<AdminTrackedProject>()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (project is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND, ErrorCode.ADMIN_TRACKED_PROJECT_NOT_FOUND);
            }

            project.IsActive = false;
            project.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<AdminTrackedProject>().Update(project);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<NoContentDto>.SuccessResponse(new NoContentDto());
        }

        private async Task<AdminTrackedProjectDto> MapToDtoAsync(AdminTrackedProject project, CancellationToken ct)
        {
            var offers = await _uow.Repository<AdminTrackedProjectOffer>()
                .ListNoTrackingAsync(x => x.AdminTrackedProjectId == project.Id && x.IsActive, ct);
            var attachments = await _uow.Repository<AdminTrackedProjectAttachment>()
                .ListNoTrackingAsync(x => x.AdminTrackedProjectId == project.Id && x.IsActive, ct);
            var latestApprovalRequest = (await _uow.Repository<StakeholderApprovalRequest>()
                .ListNoTrackingAsync(x => x.AdminTrackedProjectId == project.Id, ct))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
            var approvalRecipients = latestApprovalRequest is null
                ? []
                : await _uow.Repository<StakeholderApprovalRecipient>()
                    .ListNoTrackingAsync(x => x.StakeholderApprovalRequestId == latestApprovalRequest.Id, ct);

            return new AdminTrackedProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Location = project.Location,
                PersonName = project.PersonName,
                PhoneNumber = project.PhoneNumber,
                ProjectDate = project.ProjectDate,
                InquiryReceivedAt = project.InquiryReceivedAt,
                OfferSentAt = project.OfferSentAt,
                ResponseExpectedAt = project.ResponseExpectedAt,
                SystemType = project.SystemType,
                CurrentStatus = project.CurrentStatus,
                SmallNote = project.SmallNote,
                OfferPrice = project.OfferPrice,
                IsOfferPriceManual = project.IsOfferPriceManual,
                IncludesAdv = project.IncludesAdv,
                Description = project.Description,
                StakeholderApprovalStatus = project.StakeholderApprovalStatus,
                StakeholderApprovalRequestedAt = project.StakeholderApprovalRequestedAt,
                StakeholderApprovalResolvedAt = project.StakeholderApprovalResolvedAt,
                StakeholderApprovalRequestId = latestApprovalRequest?.Id,
                StakeholderApprovalDeliveredRecipientCount = approvalRecipients.Count(x => x.DeliveryStatus == "Delivered"),
                StakeholderApprovalFailedRecipientCount = approvalRecipients.Count(x => x.DeliveryStatus == "Failed"),
                StakeholderApprovalFailureStatuses = approvalRecipients.Where(x => x.DeliveryStatus == "Failed" && !string.IsNullOrWhiteSpace(x.FailureStatus)).Select(x => x.FailureStatus).Distinct().Take(3).ToList(),
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                Offers = offers.OrderBy(x => x.Id).Select(x => new AdminTrackedProjectOfferDto
                {
                    Id = x.Id,
                    Power = x.Power,
                    MountType = x.MountType,
                    AreaType = x.AreaType,
                    ExtraAmount = x.ExtraAmount,
                    SentAt = x.SentAt
                }).ToList(),
                Attachments = attachments.OrderBy(x => x.Id).Select(x => new AdminTrackedProjectAttachmentDto
                {
                    Id = x.Id,
                    FileName = x.FileName,
                    FilePath = x.FilePath,
                    Label = x.Label
                }).ToList()
            };
        }

        private static void ApplyRequest(AdminTrackedProject project, AdminTrackedProjectUpsertRequest request, DateTime now)
        {
            project.Name = request.Name.Trim();
            project.Location = Normalize(request.Location);
            project.PersonName = Normalize(request.PersonName);
            project.PhoneNumber = Normalize(request.PhoneNumber).Replace("+994", string.Empty).Trim();
            project.ProjectDate = request.ProjectDate;
            project.InquiryReceivedAt = request.InquiryReceivedAt;
            project.OfferSentAt = request.Offers?
                .Where(x => x.SentAt.HasValue)
                .Select(x => x.SentAt)
                .Max() ?? request.OfferSentAt;
            project.ResponseExpectedAt = request.ResponseExpectedAt;
            project.SystemType = request.SystemType;
            project.CurrentStatus = Normalize(request.CurrentStatus);
            project.SmallNote = Normalize(request.SmallNote, 140);
            // Tracker offers are always published with 18% ƏDV.  The incoming
            // amount remains the pre-ƏDV subtotal so older admin clients also
            // receive the required total without relying on a UI checkbox.
            project.OfferPrice = decimal.Round(Math.Max(0, request.OfferPrice) * (1 + VatRate), 2, MidpointRounding.AwayFromZero);
            project.IsOfferPriceManual = false;
            project.IncludesAdv = true;
            project.Description = Normalize(request.Description, 4000);
            project.UpdatedAt = now;
        }

        private static List<AdminTrackedProjectOffer> BuildOffers(AdminTrackedProjectUpsertRequest request)
            => request.Offers
                .Where(x => x.Power > 0 || !string.IsNullOrWhiteSpace(x.AreaType))
                .Select(x => new AdminTrackedProjectOffer
                {
                    Power = Math.Max(0, x.Power),
                    MountType = AllowedMountTypes.Contains(Normalize(x.MountType)) ? Normalize(x.MountType) : "roof",
                    AreaType = Normalize(x.AreaType, 120),
                    ExtraAmount = Math.Max(0, x.ExtraAmount),
                    SentAt = x.SentAt,
                    IsActive = true
                })
                .ToList();

        private static List<AdminTrackedProjectAttachment> BuildAttachments(AdminTrackedProjectUpsertRequest request)
            => request.Attachments
                .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                .Select(x => new AdminTrackedProjectAttachment
                {
                    FileName = string.IsNullOrWhiteSpace(x.FileName) ? "Document.pdf" : Normalize(x.FileName, 260),
                    FilePath = x.FilePath.Trim(),
                    Label = Normalize(x.Label, 120),
                    IsActive = true
                })
                .ToList();

        private static string ValidateRequest(AdminTrackedProjectUpsertRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Name))
            {
                return "Name is required.";
            }

            if (request.Offers is null || request.Offers.Count == 0 || !request.Offers.Any(x => x.Power > 0))
            {
                return "At least one project offer with power is required.";
            }

            if (request.SmallNote?.Length > 140)
            {
                return "Small note cannot exceed 140 characters.";
            }

            if (request.Attachments?.Any(x => string.IsNullOrWhiteSpace(x.FilePath)) == true)
            {
                return "Attachment file path is required.";
            }

            return null;
        }

        private static string Normalize(string value, int maxLength = 0)
        {
            var normalized = (value ?? string.Empty).Trim();
            return maxLength > 0 && normalized.Length > maxLength
                ? normalized[..maxLength]
                : normalized;
        }

        private static bool IsAccepted(string? status)
        {
            var normalized = Normalize(status ?? string.Empty).ToLowerInvariant().Replace('ə', 'e');
            return normalized == "qebul edildi";
        }
    }
}
