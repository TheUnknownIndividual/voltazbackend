using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volt.API.Services;
using Volt.Application.Common;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Infrastructure.Data;

namespace Volt.API.Controllers
{
    public sealed record SocialPostDto(
        int Id, string SourceType, int SourceId, string Platform, string Status, string? Caption, string? ImageUrl,
        string? LinkUrl, string? TopicKey, int? QualityScore, string? RejectReason, string? PermalinkUrl,
        int Attempts, DateTime CreatedAt, DateTime? PostedAt);

    public sealed record SocialPostingStatusDto(
        bool Enabled, bool DryRun, bool Paused, int PostedToday, int MaxPostsPerDay,
        bool FacebookConfigured, bool InstagramConfigured, int? InstagramTokenAgeDays, bool InstagramTokenNearExpiry);

    public sealed record SocialPostingPauseRequest(bool Paused);

    public sealed record LinkedInAuthorizeUrlDto(string Url);
    public sealed record LinkedInStatusDto(
        bool AppConfigured, bool Connected, string? OrganizationUrn,
        int? AccessTokenAgeDays, bool NeedsReconnect);

    // Admin-only visibility and an instant pause switch for the automatic Facebook/Instagram posting.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = nameof(Role.Admin))]
    public sealed class SocialPostsController : CustomBaseController
    {
        private readonly DataContext _context;
        private readonly SocialPostingOptions _options;
        private readonly LinkedInSocialPublisher _linkedIn;
        private readonly LinkedInOAuthStateStore _linkedInStates;

        public SocialPostsController(
            DataContext context, IOptions<SocialPostingOptions> options,
            LinkedInSocialPublisher linkedIn, LinkedInOAuthStateStore linkedInStates)
        {
            _context = context;
            _options = options.Value;
            _linkedIn = linkedIn;
            _linkedInStates = linkedInStates;
        }

        [HttpGet("status")]
        [EnableRateLimiting("marketplace-prepare")]
        public async Task<IActionResult> GetStatus(CancellationToken ct)
        {
            var paused = await _context.SocialPostingState.AsNoTracking().AnyAsync(x => x.Id == 1 && x.Paused, ct);
            var dayStartUtc = AzerbaijanClock.ToUtc(AzerbaijanClock.Now.Date);
            var postedToday = await _context.SocialPosts.AsNoTracking()
                .CountAsync(x => x.Platform == "facebook" && x.CreatedAt >= dayStartUtc
                                 && (x.Status == "published" || x.Status == "dryrun" || x.Status == "publishing"), ct);

            int? tokenAge = _options.InstagramTokenIssuedOn is { } issued
                ? (int)Math.Max(0, (DateTime.UtcNow - issued.ToUniversalTime()).TotalDays)
                : null;

            var dto = new SocialPostingStatusDto(
                _options.Enabled, _options.DryRun, paused, postedToday, _options.MaxPostsPerDay,
                !string.IsNullOrWhiteSpace(_options.FacebookPageId) && !string.IsNullOrWhiteSpace(_options.FacebookPageAccessToken),
                !string.IsNullOrWhiteSpace(_options.InstagramUserId) && !string.IsNullOrWhiteSpace(_options.InstagramAccessToken),
                tokenAge, tokenAge is >= 45);
            return CreateActionResult(ApiResponse<SocialPostingStatusDto>.SuccessResponse(dto));
        }

        [HttpGet]
        [EnableRateLimiting("marketplace-prepare")]
        public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int take = 60, CancellationToken ct = default)
        {
            var query = _context.SocialPosts.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
            var rows = await query
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Take(Math.Clamp(take, 1, 200))
                .Select(x => new SocialPostDto(
                    x.Id, x.SourceType, x.SourceId, x.Platform, x.Status, x.Caption, x.ImageUrl, x.LinkUrl, x.TopicKey,
                    x.QualityScore, x.RejectReason, x.PermalinkUrl, x.Attempts, x.CreatedAt, x.PostedAt))
                .ToListAsync(ct);
            return CreateActionResult(ApiResponse<List<SocialPostDto>>.SuccessResponse(rows));
        }

        [HttpPost("pause")]
        [EnableRateLimiting("marketplace-prepare")]
        public async Task<IActionResult> SetPaused([FromBody] SocialPostingPauseRequest request, CancellationToken ct)
        {
            var state = await _context.SocialPostingState.FirstOrDefaultAsync(x => x.Id == 1, ct);
            if (state is null)
            {
                state = new SocialPostingState { Id = 1 };
                _context.SocialPostingState.Add(state);
            }
            state.Paused = request.Paused;
            state.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return CreateActionResult(ApiResponse<bool>.SuccessResponse(state.Paused));
        }

        [HttpGet("linkedin/authorize-url")]
        [EnableRateLimiting("marketplace-prepare")]
        public IActionResult GetLinkedInAuthorizeUrl()
        {
            if (!_linkedIn.AppConfigured)
                return CreateActionResult(ApiResponse<LinkedInAuthorizeUrlDto>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR, "LINKEDIN_APP_NOT_CONFIGURED"));
            var state = _linkedInStates.Create();
            var url = _linkedIn.BuildAuthorizeUrl(state);
            return CreateActionResult(ApiResponse<LinkedInAuthorizeUrlDto>.SuccessResponse(new LinkedInAuthorizeUrlDto(url)));
        }

        [HttpGet("linkedin/status")]
        [EnableRateLimiting("marketplace-prepare")]
        public async Task<IActionResult> GetLinkedInStatus(CancellationToken ct)
        {
            var status = await _linkedIn.GetStatusAsync(ct);
            int? ageDays = status.AccessTokenExpiresAtUtc is { } expiresAt
                ? (int?)Math.Max(0, (DateTime.UtcNow - (expiresAt - TimeSpan.FromDays(60))).TotalDays)
                : null;
            var dto = new LinkedInStatusDto(status.AppConfigured, status.Connected, status.OrganizationUrn, ageDays, status.NeedsReconnect);
            return CreateActionResult(ApiResponse<LinkedInStatusDto>.SuccessResponse(dto));
        }

        // LinkedIn redirects the admin's browser straight here after they approve the connection;
        // there is no app JWT on this request, so it must stay anonymous. It accepts nothing beyond
        // the one-time authorization code and a CSRF state token, and never returns JSON -- just a
        // small HTML page for the admin to close.
        [AllowAnonymous]
        [HttpGet("linkedin/callback")]
        public async Task<IActionResult> LinkedInCallback([FromQuery] string? code, [FromQuery] string? state, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code) || !_linkedInStates.TryConsume(state ?? string.Empty))
                return Content(CallbackPage("Bağlantı alınmadı (vaxtı bitmiş keçid). Pəncərəni bağlayıb yenidən cəhd edin."), "text/html");

            var adminId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
            var (ok, error) = await _linkedIn.ConnectAsync(code, adminId, ct);
            return Content(CallbackPage(ok
                ? "LinkedIn qoşuldu! Bu pəncərəni bağlaya bilərsiniz."
                : $"Bağlantı alınmadı: {error}"), "text/html");
        }

        private static string CallbackPage(string message)
            => $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Volt.az</title></head>" +
               $"<body style=\"font-family:sans-serif;padding:40px;text-align:center;\"><p>{message}</p></body></html>";
    }
}
