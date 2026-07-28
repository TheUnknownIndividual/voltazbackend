using Volt.Application.Dtos;
using Volt.Application.Dtos.Admin;

namespace Volt.Application.Interfaces;

public interface IAdminTelegramConnectionService
{
    Task<ApiResponse<TelegramConnectionLinkDto>> CreateLinkAsync(int adminUserId, CancellationToken ct = default);
    Task<ApiResponse<NoContentDto>> RedeemAsync(TelegramConnectionRedeemRequest request, CancellationToken ct = default);
}
