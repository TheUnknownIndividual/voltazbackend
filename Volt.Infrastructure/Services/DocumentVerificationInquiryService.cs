using Microsoft.EntityFrameworkCore;
using Volt.Application.Dtos;
using Volt.Application.Dtos.AdminProjectTracker;
using Volt.Application.Dtos.Verification;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services
{
    public sealed class DocumentVerificationInquiryService : IDocumentVerificationInquiryService
    {
        private readonly DataContext _context;
        private readonly IAdminAccessService _access;
        private readonly IAdminAuditService _audit;

        public DocumentVerificationInquiryService(DataContext context, IAdminAccessService access, IAdminAuditService audit)
        {
            _context = context;
            _access = access;
            _audit = audit;
        }

        public async Task<ApiResponse<PublicDocumentVerificationInquiryDto>> CreatePublicAsync(string token, PublicDocumentVerificationInquiryRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
                return ApiResponse<PublicDocumentVerificationInquiryDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Verification record was not found.");

            var type = NormalizeType(request?.Type);
            var comment = (request?.Comment ?? string.Empty).Trim();
            if (type is null || comment.Length > 2000 || (type == "question" && comment.Length == 0))
                return ApiResponse<PublicDocumentVerificationInquiryDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Inquiry details are invalid.");

            var verification = await _context.DocumentVerifications
                .Include(x => x.DocumentLog)
                .FirstOrDefaultAsync(x => x.PublicToken == token, ct);
            if (verification is null)
                return ApiResponse<PublicDocumentVerificationInquiryDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Verification record was not found.");

            var status = GetVerificationStatus(verification);
            if (!IsAllowedForStatus(type, status))
                return ApiResponse<PublicDocumentVerificationInquiryDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "This inquiry is not available for the document status.");

            var now = DateTime.UtcNow;
            var inquiry = new DocumentVerificationInquiry
            {
                DocumentVerificationId = verification.Id,
                AdminTrackedProjectId = verification.DocumentLog.AdminTrackedProjectId,
                Type = type,
                Comment = comment,
                Status = "new",
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.DocumentVerificationInquiries.Add(inquiry);
            await _context.SaveChangesAsync(ct);
            try
            {
                await _audit.WriteAsync(null, null, "VERIFICATION_INQUIRY_CREATED", "Document", verification.DocumentLogId.ToString(), type, true, ct);
            }
            catch
            {
                // A public request must not be discarded merely because its audit write fails.
            }
            return ApiResponse<PublicDocumentVerificationInquiryDto>.SuccessResponse(new PublicDocumentVerificationInquiryDto(inquiry.Id, inquiry.Status));
        }

        public async Task<ApiResponse<VerificationInquiryPageDto>> GetPageAsync(int actorAdminUserId, string? type, string? status, int? assignedAdminUserId, int page, int pageSize, CancellationToken ct = default)
        {
            var session = await _access.GetSessionAsync(actorAdminUserId, ct);
            if (session is null)
                return ApiResponse<VerificationInquiryPageDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Admin session was not found.");

            var query = _context.DocumentVerificationInquiries.AsNoTracking()
                .Include(x => x.DocumentVerification).ThenInclude(x => x.DocumentLog).ThenInclude(x => x.AdminTrackedProject)
                .Include(x => x.AssignedAdminUser)
                .AsQueryable();
            if (!session.IsSuperAdmin) query = query.Where(x => x.AssignedAdminUserId == actorAdminUserId);
            if (NormalizeType(type) is { } normalizedType) query = query.Where(x => x.Type == normalizedType);
            if (NormalizeStatus(status) is { } normalizedStatus) query = query.Where(x => x.Status == normalizedStatus);
            if (session.IsSuperAdmin && assignedAdminUserId.HasValue) query = query.Where(x => x.AssignedAdminUserId == assignedAdminUserId.Value);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var total = await query.CountAsync(ct);
            var records = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return ApiResponse<VerificationInquiryPageDto>.SuccessResponse(new VerificationInquiryPageDto
            {
                Items = records.Select(MapList).ToList(), Total = total, Page = page, PageSize = pageSize
            });
        }

        public async Task<ApiResponse<VerificationInquiryDetailDto>> GetDetailAsync(int id, int actorAdminUserId, CancellationToken ct = default)
        {
            var inquiry = await FindDetailedAsync(id, ct);
            if (inquiry is null || !await CanSeeAsync(inquiry, actorAdminUserId, ct))
                return ApiResponse<VerificationInquiryDetailDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Inquiry was not found.");
            return ApiResponse<VerificationInquiryDetailDto>.SuccessResponse(MapDetail(inquiry));
        }

        public async Task<ApiResponse<VerificationInquiryDetailDto>> AssignAsync(int id, int? assignedAdminUserId, int actorAdminUserId, CancellationToken ct = default)
        {
            var inquiry = await FindDetailedAsync(id, ct);
            if (inquiry is null)
                return ApiResponse<VerificationInquiryDetailDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Inquiry was not found.");

            AdminUser assignee = null;
            if (assignedAdminUserId.HasValue)
            {
                assignee = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == assignedAdminUserId.Value && x.IsActive, ct);
                if (assignee is null)
                    return ApiResponse<VerificationInquiryDetailDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Selected admin user was not found.");
            }

            var now = DateTime.UtcNow;
            inquiry.AssignedAdminUserId = assignee?.Id;
            inquiry.AssignedAdminUser = assignee;
            inquiry.AssignedAt = assignee is null ? null : now;
            inquiry.UpdatedAt = now;
            await _context.SaveChangesAsync(ct);
            var actor = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorAdminUserId, ct);
            await _audit.WriteAsync(actorAdminUserId, actor?.Username, "VERIFICATION_INQUIRY_ASSIGNED", "VerificationInquiry", id.ToString(), assignee?.Id.ToString() ?? "unassigned", true, ct);
            return ApiResponse<VerificationInquiryDetailDto>.SuccessResponse(MapDetail(inquiry));
        }

        public async Task<ApiResponse<VerificationInquiryDetailDto>> UpdateStatusAsync(int id, string status, int actorAdminUserId, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus is null)
                return ApiResponse<VerificationInquiryDetailDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Inquiry status is invalid.");

            var inquiry = await FindDetailedAsync(id, ct);
            if (inquiry is null || !await CanSeeAsync(inquiry, actorAdminUserId, ct))
                return ApiResponse<VerificationInquiryDetailDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Inquiry was not found.");

            var now = DateTime.UtcNow;
            inquiry.Status = normalizedStatus;
            inquiry.UpdatedAt = now;
            inquiry.ResolvedAt = normalizedStatus == "resolved" ? now : null;
            await _context.SaveChangesAsync(ct);
            var actor = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorAdminUserId, ct);
            await _audit.WriteAsync(actorAdminUserId, actor?.Username, "VERIFICATION_INQUIRY_STATUS_UPDATED", "VerificationInquiry", id.ToString(), normalizedStatus, true, ct);
            return ApiResponse<VerificationInquiryDetailDto>.SuccessResponse(MapDetail(inquiry));
        }

        private async Task<DocumentVerificationInquiry?> FindDetailedAsync(int id, CancellationToken ct) => await _context.DocumentVerificationInquiries
            .Include(x => x.AssignedAdminUser)
            .Include(x => x.AdminTrackedProject).ThenInclude(x => x.Offers)
            .Include(x => x.AdminTrackedProject).ThenInclude(x => x.Attachments)
            .Include(x => x.DocumentVerification).ThenInclude(x => x.DocumentLog).ThenInclude(x => x.AdminTrackedProject).ThenInclude(x => x.Offers)
            .Include(x => x.DocumentVerification).ThenInclude(x => x.DocumentLog).ThenInclude(x => x.AdminTrackedProject).ThenInclude(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        private async Task<bool> CanSeeAsync(DocumentVerificationInquiry inquiry, int actorAdminUserId, CancellationToken ct)
        {
            var session = await _access.GetSessionAsync(actorAdminUserId, ct);
            return session?.IsSuperAdmin == true || inquiry.AssignedAdminUserId == actorAdminUserId;
        }

        private static VerificationInquiryListItemDto MapList(DocumentVerificationInquiry inquiry) => new()
        {
            Id = inquiry.Id, Type = inquiry.Type, Status = inquiry.Status, Comment = inquiry.Comment, CreatedAt = inquiry.CreatedAt,
            AssignedAdminUserId = inquiry.AssignedAdminUserId,
            AssignedAdminDisplayName = inquiry.AssignedAdminUser?.DisplayName ?? inquiry.AssignedAdminUser?.Username,
            DocumentLogId = inquiry.DocumentVerification.DocumentLogId,
            DocumentNumber = inquiry.DocumentVerification.DocumentNumber,
            DocumentCode = inquiry.DocumentVerification.DocumentCode,
            VerificationStatus = GetVerificationStatus(inquiry.DocumentVerification),
            AdminTrackedProjectId = inquiry.AdminTrackedProjectId,
            AdminTrackedProjectName = inquiry.AdminTrackedProject?.Name ?? inquiry.DocumentVerification.DocumentLog?.AdminTrackedProject?.Name
        };

        private static VerificationInquiryDetailDto MapDetail(DocumentVerificationInquiry inquiry)
        {
            var item = MapList(inquiry);
            var linkedProject = inquiry.AdminTrackedProject ?? inquiry.DocumentVerification.DocumentLog?.AdminTrackedProject;
            return new VerificationInquiryDetailDto
            {
                Id = item.Id, Type = item.Type, Status = item.Status, Comment = item.Comment, CreatedAt = item.CreatedAt,
                AssignedAdminUserId = item.AssignedAdminUserId, AssignedAdminDisplayName = item.AssignedAdminDisplayName,
                DocumentLogId = item.DocumentLogId, DocumentNumber = item.DocumentNumber, DocumentCode = item.DocumentCode,
                VerificationStatus = item.VerificationStatus, AdminTrackedProjectId = item.AdminTrackedProjectId, AdminTrackedProjectName = item.AdminTrackedProjectName,
                IssuerDisplayName = inquiry.DocumentVerification.IssuerDisplayName,
                IssuedAt = inquiry.DocumentVerification.IssuedAt,
                ExpiresAt = inquiry.DocumentVerification.ExpiresAt,
                RevokedAt = inquiry.DocumentVerification.RevokedAt,
                RevocationReason = inquiry.DocumentVerification.RevocationReason,
                DocumentPayloadJson = inquiry.DocumentVerification.DocumentLog?.PayloadJson ?? string.Empty,
                Project = linkedProject is null ? null : MapProject(linkedProject)
            };
        }

        private static AdminTrackedProjectDto MapProject(AdminTrackedProject project) => new()
        {
            Id = project.Id, Name = project.Name, Location = project.Location, PersonName = project.PersonName, PhoneNumber = project.PhoneNumber,
            ProjectDate = project.ProjectDate, InquiryReceivedAt = project.InquiryReceivedAt, OfferSentAt = project.OfferSentAt,
            ResponseExpectedAt = project.ResponseExpectedAt, SystemType = project.SystemType, CurrentStatus = project.CurrentStatus,
            SmallNote = project.SmallNote, OfferPrice = project.OfferPrice, IsOfferPriceManual = project.IsOfferPriceManual,
            IncludesAdv = project.IncludesAdv, Description = project.Description, CreatedAt = project.CreatedAt, UpdatedAt = project.UpdatedAt,
            Offers = project.Offers.OrderBy(x => x.Id).Select(x => new AdminTrackedProjectOfferDto { Id = x.Id, Power = x.Power, MountType = x.MountType, AreaType = x.AreaType, ExtraAmount = x.ExtraAmount, SentAt = x.SentAt }).ToList(),
            Attachments = project.Attachments.OrderBy(x => x.Id).Select(x => new AdminTrackedProjectAttachmentDto { Id = x.Id, FileName = x.FileName, FilePath = x.FilePath, Label = x.Label }).ToList()
        };

        private static string? NormalizeType(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "question" => "question", "renewal" => "renewal", "revocation-review" => "revocation-review", _ => null
        };
        private static string? NormalizeStatus(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "new" => "new", "in-progress" => "in-progress", "resolved" => "resolved", _ => null
        };
        private static bool IsAllowedForStatus(string type, string status) =>
            (type == "question" && status == "valid") || (type == "renewal" && status == "expired") || (type == "revocation-review" && status == "revoked");
        private static string GetVerificationStatus(DocumentVerification record) => record.RevokedAt.HasValue ? "revoked" : record.ExpiresAt < GetAzerbaijanNow() ? "expired" : "valid";
        private static DateTime GetAzerbaijanNow()
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku")); }
            catch { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time")); }
        }
    }
}
