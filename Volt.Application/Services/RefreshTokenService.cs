using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Volt.Application.Dtos.AuthRefresh;
using Volt.Application.Dtos.CustomerAuth;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class RefreshTokenService : IRefreshTokenService
    {
        private readonly IUnitOfWork _uow;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;

        public RefreshTokenService(
            IUnitOfWork uow,
            ITokenService tokenService,
            IConfiguration configuration)
        {
            _uow = uow;
            _tokenService = tokenService;
            _configuration = configuration;
        }

        public async Task<string> IssueAsync(Role role, int userId, CancellationToken ct = default)
        {
            var rawToken = GenerateToken();
            var now = DateTime.UtcNow;
            var token = new AuthRefreshToken
            {
                Id = Guid.NewGuid(),
                TokenHash = HashToken(rawToken),
                Role = role,
                UserId = userId,
                CreatedAt = now,
                ExpiresAt = now.AddDays(GetRefreshLifetimeDays(role)),
                SessionStartedAt = now,
            };

            await _uow.Repository<AuthRefreshToken>().AddAsync(token, ct);
            await _uow.SaveChangesAsync(ct);
            return rawToken;
        }

        public async Task<RefreshSessionResult?> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            var repository = _uow.Repository<AuthRefreshToken>();
            var current = await repository.FirstOrDefaultAsync(
                x => x.TokenHash == HashToken(refreshToken) &&
                     x.RevokedAt == null &&
                     x.ExpiresAt > DateTime.UtcNow,
                ct);

            if (current is null) return null;

            // Enforce an absolute session ceiling tied to the ORIGINAL login, not the
            // most recent rotation, so an active admin session still forces a fresh
            // login after GetAbsoluteSessionLifetimeDays() days instead of renewing
            // forever just because the user stayed active.
            var sessionAgeDays = (DateTime.UtcNow - current.SessionStartedAt).TotalDays;
            if (sessionAgeDays > GetAbsoluteSessionLifetimeDays(current.Role))
            {
                return null;
            }

            var replacementRawToken = GenerateToken();
            var replacement = new AuthRefreshToken
            {
                Id = Guid.NewGuid(),
                TokenHash = HashToken(replacementRawToken),
                Role = current.Role,
                UserId = current.UserId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshLifetimeDays(current.Role)),
                SessionStartedAt = current.SessionStartedAt,
            };

            current.RevokedAt = DateTime.UtcNow;
            current.ReplacedByTokenId = replacement.Id;
            await repository.AddAsync(replacement, ct);

            var wonRotationRace = true;

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request (e.g. a second browser tab) rotated this exact
                // refresh token a moment earlier. That's not an invalid session -
                // it's the same login racing itself. Recognize the token as already
                // rotated and issue a fresh access token from it instead of forcing
                // a full logout; the winning request's Set-Cookie already carries
                // the new refresh token for this browser.
                var alreadyRotated = await repository.FirstOrDefaultNoTrackingAsync(
                    x => x.TokenHash == current.TokenHash &&
                         x.RevokedAt != null &&
                         x.ReplacedByTokenId != null,
                    ct);

                if (alreadyRotated is null) return null;

                wonRotationRace = false;
                current = alreadyRotated;
            }

            if (current.Role == Role.Admin)
            {
                var admin = await _uow.Repository<AdminUser>()
                    .FirstOrDefaultNoTrackingAsync(x => x.Id == current.UserId && x.IsActive, ct);
                if (admin is null) return null;

                return new RefreshSessionResult(
                    Role.Admin,
                    _tokenService.CreateAdminAccessToken(admin).AccessToken,
                    null,
                    wonRotationRace ? replacementRawToken : null,
                    IssuedFromConcurrentRotation: !wonRotationRace);
            }

            if (current.Role == Role.Customer)
            {
                var customer = await _uow.Repository<CustomerUser>()
                    .FirstOrDefaultNoTrackingAsync(x => x.Id == current.UserId && x.IsActive, ct);
                if (customer is null) return null;

                return new RefreshSessionResult(
                    Role.Customer,
                    _tokenService.CreateCustomerAccessToken(customer).AccessToken,
                    new CustomerDto(
                        customer.Id,
                        customer.FirstName,
                        customer.LastName,
                        $"{customer.FirstName} {customer.LastName}".Trim(),
                        customer.Email,
                        customer.Phone ?? string.Empty,
                        customer.Address ?? string.Empty,
                        customer.Role.ToString(),
                        customer.CreatedAt),
                    wonRotationRace ? replacementRawToken : null,
                    IssuedFromConcurrentRotation: !wonRotationRace);
            }

            return null;
        }

        public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return;

            var token = await _uow.Repository<AuthRefreshToken>()
                .FirstOrDefaultAsync(x => x.TokenHash == HashToken(refreshToken) && x.RevokedAt == null, ct);

            if (token is null) return;

            token.RevokedAt = DateTime.UtcNow;
            _uow.Repository<AuthRefreshToken>().Update(token);
            await _uow.SaveChangesAsync(ct);
        }

        private int GetRefreshLifetimeDays(Role role)
        {
            if (role == Role.Admin &&
                int.TryParse(_configuration["TokenOptions:AdminRefreshTokenDays"], out var adminDays))
            {
                return Math.Max(1, adminDays);
            }

            return int.TryParse(_configuration["TokenOptions:RefreshTokenDays"], out var days)
                ? Math.Max(1, days)
                : 30;
        }

        // The hard ceiling for a single continuous session, regardless of activity.
        // Defaults to the rolling refresh window itself (today's behavior) unless a
        // shorter absolute cap is configured - admins get a 7-day cap by default.
        private int GetAbsoluteSessionLifetimeDays(Role role)
        {
            if (role == Role.Admin)
            {
                return int.TryParse(_configuration["TokenOptions:AdminAbsoluteSessionDays"], out var adminAbsoluteDays)
                    ? Math.Max(1, adminAbsoluteDays)
                    : 7;
            }

            return int.TryParse(_configuration["TokenOptions:AbsoluteSessionDays"], out var absoluteDays)
                ? Math.Max(1, absoluteDays)
                : GetRefreshLifetimeDays(role);
        }

        private static string GenerateToken()
            => Base64Url(RandomNumberGenerator.GetBytes(64));

        private static string HashToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static string Base64Url(byte[] value)
            => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
