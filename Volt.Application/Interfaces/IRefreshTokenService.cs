using Volt.Application.Dtos.AuthRefresh;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<string> IssueAsync(Role role, int userId, CancellationToken ct = default);
        Task<RefreshSessionResult?> RefreshAsync(string refreshToken, CancellationToken ct = default);
        Task RevokeAsync(string refreshToken, CancellationToken ct = default);
    }
}
