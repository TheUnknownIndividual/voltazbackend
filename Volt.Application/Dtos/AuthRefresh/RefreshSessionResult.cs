using Volt.Domain.Enums;
using Volt.Application.Dtos.CustomerAuth;

namespace Volt.Application.Dtos.AuthRefresh
{
    public sealed record RefreshSessionResult(
        Role Role,
        string AccessToken,
        CustomerDto User,
        string RefreshToken);
}
