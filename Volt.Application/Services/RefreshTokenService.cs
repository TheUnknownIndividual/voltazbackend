using System.Security.Cryptography;
using System.Text;
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
                ExpiresAt = now.AddDays(GetRefreshLifetimeDays()),
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

            var replacementRawToken = GenerateToken();
            var replacement = new AuthRefreshToken
            {
                Id = Guid.NewGuid(),
                TokenHash = HashToken(replacementRawToken),
                Role = current.Role,
                UserId = current.UserId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshLifetimeDays()),
            };

            current.RevokedAt = DateTime.UtcNow;
            current.ReplacedByTokenId = replacement.Id;
            await repository.AddAsync(replacement, ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                // A rotated token is single-use. A concurrent refresh loses safely.
                return null;
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
                    replacementRawToken);
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
                    replacementRawToken);
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

        private int GetRefreshLifetimeDays()
            => int.TryParse(_configuration["TokenOptions:RefreshTokenDays"], out var days)
                ? Math.Max(1, days)
                : 30;

        private static string GenerateToken()
            => Base64Url(RandomNumberGenerator.GetBytes(64));

        private static string HashToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static string Base64Url(byte[] value)
            => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
