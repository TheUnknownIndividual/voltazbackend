using System.Security.Cryptography;
using System.Text;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Admin;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services;

public sealed class AdminTelegramConnectionService : IAdminTelegramConnectionService
{
    private readonly IUnitOfWork _uow;
    private readonly TelegramBotOptions _options;
    public AdminTelegramConnectionService(IUnitOfWork uow, Microsoft.Extensions.Options.IOptions<TelegramBotOptions> options) { _uow = uow; _options = options.Value; }

    public async Task<ApiResponse<TelegramConnectionLinkDto>> CreateLinkAsync(int adminUserId, CancellationToken ct = default)
    {
        var username = (_options.BotUsername ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(username)) return Invalid<TelegramConnectionLinkDto>("Telegram bot username has not been configured on the API server.");
        if (!await _uow.Repository<AdminUser>().AnyAsync(x => x.Id == adminUserId && x.IsActive, ct)) return Invalid<TelegramConnectionLinkDto>("The selected admin account is not active.");

        var rawToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var token = new AdminTelegramConnectionToken
        {
            AdminUserId = adminUserId,
            TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15)
        };
        await _uow.Repository<AdminTelegramConnectionToken>().AddAsync(token, ct);
        await _uow.SaveChangesAsync(ct);
        var scope = (_options.ConnectionLinkScope ?? "prod").Trim().ToLowerInvariant();
        var payload = scope == "test" ? $"connect_test_{rawToken}" : $"connect_{rawToken}";
        return ApiResponse<TelegramConnectionLinkDto>.SuccessResponse(new TelegramConnectionLinkDto($"https://t.me/{username}?start={payload}", token.ExpiresAt));
    }

    public async Task<ApiResponse<NoContentDto>> RedeemAsync(TelegramConnectionRedeemRequest request, CancellationToken ct = default)
    {
        if (request is null || request.ChatId == 0 || string.IsNullOrWhiteSpace(request.Token)) return Invalid<NoContentDto>("A valid Telegram connection token and chat ID are required.");
        byte[] hash;
        try { hash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Token.Trim())); }
        catch { return Invalid<NoContentDto>("The connection link is invalid."); }
        var token = await _uow.Repository<AdminTelegramConnectionToken>().FirstOrDefaultAsync(x => x.TokenHash == hash && x.RedeemedAt == null && x.ExpiresAt > DateTime.UtcNow, ct);
        if (token is null) return Invalid<NoContentDto>("This Telegram connection link is invalid or has expired. Generate a new link.");
        var account = await _uow.Repository<AdminUser>().FirstOrDefaultAsync(x => x.Id == token.AdminUserId && x.IsActive, ct);
        if (account is null) return Invalid<NoContentDto>("The linked admin account is no longer active.");
        var linkedElsewhere = await _uow.Repository<AdminUser>().AnyAsync(x => x.Id != account.Id && x.TelegramChatId == request.ChatId, ct);
        if (linkedElsewhere) return Invalid<NoContentDto>("This Telegram account is already linked to another Volt user.");

        account.TelegramChatId = request.ChatId;
        token.RedeemedAt = DateTime.UtcNow;
        token.RedeemedChatId = request.ChatId;
        _uow.Repository<AdminUser>().Update(account);
        _uow.Repository<AdminTelegramConnectionToken>().Update(token);
        await _uow.SaveChangesAsync(ct);
        return ApiResponse<NoContentDto>.SuccessResponse(new NoContentDto());
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static ApiResponse<T> Invalid<T>(string message) => ApiResponse<T>.ErrorResponse(ErrorCode.VALIDATION_ERROR, message);
}
