using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Verification;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services
{
    public sealed class DocumentVerificationService : IDocumentVerificationService
    {
        private const string VerificationBaseUrl = "https://verification.volt.az/v/";
        private readonly DataContext _context;
        private readonly IAdminAuditService _audit;

        public DocumentVerificationService(DataContext context, IAdminAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<ApiResponse<IReadOnlyList<AdminDocumentVerificationDto>>> GetRecentAsync(int take = 100, CancellationToken ct = default)
        {
            var records = await _context.DocumentVerifications.AsNoTracking()
                .Include(x => x.DocumentLog).ThenInclude(x => x.AdminTrackedProject)
                .OrderByDescending(x => x.IssuedAt)
                .Take(Math.Clamp(take, 1, 200))
                .ToListAsync(ct);
            return ApiResponse<IReadOnlyList<AdminDocumentVerificationDto>>.SuccessResponse(records.Select(ToDto).ToList());
        }

        public async Task<ApiResponse<PublicDocumentVerificationDto>> GetPublicAsync(string token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
                return ApiResponse<PublicDocumentVerificationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Verification record was not found.");

            var record = await _context.DocumentVerifications.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PublicToken == token, ct);
            if (record is null)
                return ApiResponse<PublicDocumentVerificationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Verification record was not found.");

            var status = record.RevokedAt.HasValue
                ? "revoked"
                : record.ExpiresAt < GetAzerbaijanNow() ? "expired" : "valid";
            return ApiResponse<PublicDocumentVerificationDto>.SuccessResponse(new PublicDocumentVerificationDto(
                true, record.DocumentNumber, record.DocumentCode, status, record.IssuedAt, record.ExpiresAt, record.IssuerDisplayName));
        }

        public async Task<ApiResponse<AdminDocumentVerificationDto>> GetAdminAsync(int documentLogId, CancellationToken ct = default)
        {
            var record = await _context.DocumentVerifications.AsNoTracking()
                .Include(x => x.DocumentLog).ThenInclude(x => x.AdminTrackedProject)
                .FirstOrDefaultAsync(x => x.DocumentLogId == documentLogId, ct);
            return record is null
                ? ApiResponse<AdminDocumentVerificationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Verification record was not found.")
                : ApiResponse<AdminDocumentVerificationDto>.SuccessResponse(ToDto(record));
        }

        public async Task<ApiResponse<AdminDocumentVerificationDto>> ReissueAsync(int documentLogId, int adminUserId, CancellationToken ct = default)
        {
            var document = await _context.DocumentLogs.Include(x => x.AdminUser).Include(x => x.AdminTrackedProject).FirstOrDefaultAsync(x => x.Id == documentLogId, ct);
            if (document is null)
                return ApiResponse<AdminDocumentVerificationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Document was not found.");

            var now = GetAzerbaijanNow();
            var admin = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
            var record = await _context.DocumentVerifications.Include(x => x.DocumentLog).ThenInclude(x => x.AdminTrackedProject).FirstOrDefaultAsync(x => x.DocumentLogId == documentLogId, ct);
            if (record is null)
            {
                record = new DocumentVerification { DocumentLogId = documentLogId };
                _context.DocumentVerifications.Add(record);
            }
            record.DocumentLog = document;
            Populate(record, document, GetIssuerDisplayName(admin ?? document.AdminUser), now);
            await _context.SaveChangesAsync(ct);
            await _audit.WriteAsync(adminUserId, admin?.Username, "VERIFICATION_REISSUED", "Document", documentLogId.ToString(), document.DocumentNumber, true, ct);
            return ApiResponse<AdminDocumentVerificationDto>.SuccessResponse(ToDto(record));
        }

        public async Task<ApiResponse<NoContentDto>> RevokeAsync(int documentLogId, string reason, int adminUserId, CancellationToken ct = default)
        {
            reason = (reason ?? string.Empty).Trim();
            if (reason.Length is < 3 or > 1000)
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "A cancellation reason is required.");
            var record = await _context.DocumentVerifications.FirstOrDefaultAsync(x => x.DocumentLogId == documentLogId, ct);
            if (record is null) return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Verification record was not found.");
            record.RevokedAt = DateTime.UtcNow;
            record.RevocationReason = reason;
            record.RevokedByAdminUserId = adminUserId;
            await _context.SaveChangesAsync(ct);
            var admin = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
            await _audit.WriteAsync(adminUserId, admin?.Username, "VERIFICATION_REVOKED", "Document", documentLogId.ToString(), "Cancellation reason recorded", true, ct);
            return ApiResponse<NoContentDto>.SuccessResponse(null);
        }

        public async Task<ApiResponse<AdminDocumentVerificationDto>> LinkProjectAsync(int documentLogId, int adminTrackedProjectId, int adminUserId, CancellationToken ct = default)
        {
            var project = await _context.AdminTrackedProjects.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == adminTrackedProjectId && x.IsActive, ct);
            if (project is null)
                return ApiResponse<AdminDocumentVerificationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Active tracker project was not found.");

            var document = await _context.DocumentLogs.Include(x => x.AdminTrackedProject).FirstOrDefaultAsync(x => x.Id == documentLogId, ct);
            if (document is null)
                return ApiResponse<AdminDocumentVerificationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Document was not found.");
            var record = await _context.DocumentVerifications.FirstOrDefaultAsync(x => x.DocumentLogId == documentLogId, ct);
            if (record is null)
                return ApiResponse<AdminDocumentVerificationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Verification record was not found.");

            document.AdminTrackedProjectId = project.Id;
            document.AdminTrackedProject = project;
            record.DocumentLog = document;
            await _context.SaveChangesAsync(ct);
            var admin = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
            await _audit.WriteAsync(adminUserId, admin?.Username, "VERIFICATION_LINKED_TO_PROJECT", "Document", documentLogId.ToString(), project.Id.ToString(), true, ct);
            return ApiResponse<AdminDocumentVerificationDto>.SuccessResponse(ToDto(record));
        }

        public async Task<ApiResponse<IReadOnlyList<AdminDocumentVerificationDto>>> GetForProjectAsync(int adminTrackedProjectId, CancellationToken ct = default)
        {
            var records = await _context.DocumentVerifications.AsNoTracking()
                .Include(x => x.DocumentLog).ThenInclude(x => x.AdminTrackedProject)
                .Where(x => x.DocumentLog.AdminTrackedProjectId == adminTrackedProjectId)
                .OrderByDescending(x => x.IssuedAt)
                .ToListAsync(ct);
            return ApiResponse<IReadOnlyList<AdminDocumentVerificationDto>>.SuccessResponse(records.Select(ToDto).ToList());
        }

        public static void Populate(DocumentVerification record, DocumentLog document, string issuerName, DateTime issuedAt)
        {
            record.PublicToken = CreateToken();
            record.DocumentNumber = document.DocumentNumber;
            record.DocumentCode = document.DocumentCode;
            record.IssuerDisplayName = issuerName;
            record.IssuedAt = issuedAt;
            record.ExpiresAt = issuedAt.AddDays(30);
            record.RevokedAt = null;
            record.RevocationReason = null;
            record.RevokedByAdminUserId = null;
        }

        public static string GetIssuerDisplayName(AdminUser? admin)
            => string.IsNullOrWhiteSpace(admin?.DisplayName) ? "Volt.az" : admin.DisplayName.Trim();

        public static string GetPublicUrl(string token) => VerificationBaseUrl + token;

        private static AdminDocumentVerificationDto ToDto(DocumentVerification record) => new(
            record.Id, record.DocumentLogId, record.PublicToken, GetPublicUrl(record.PublicToken), record.DocumentNumber,
            record.DocumentCode, record.IssuerDisplayName, record.IssuedAt, record.ExpiresAt, record.RevokedAt,
            record.DocumentLog?.AdminTrackedProjectId, record.DocumentLog?.AdminTrackedProject?.Name, record.RevocationReason);

        private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static DateTime GetAzerbaijanNow()
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku")); }
            catch { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time")); }
        }
    }
}
