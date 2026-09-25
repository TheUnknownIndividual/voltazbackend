using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

    // Admin-only visibility and an instant pause switch for the automatic Facebook/Instagram posting.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = nameof(Role.Admin))]
    public sealed class SocialPostsController : CustomBaseController
    {
        private readonly DataContext _context;
        private readonly SocialPostingOptions _options;

        public SocialPostsController(DataContext context, IOptions<SocialPostingOptions> options)
        {
            _context = context;
            _options = options.Value;
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
    }
}
