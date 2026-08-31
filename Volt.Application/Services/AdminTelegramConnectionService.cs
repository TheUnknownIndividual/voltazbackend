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
    private readonly IAdminAuditService _audit;
    public AdminTelegramConnectionService(
        IUnitOfWork uow,
        Microsoft.Extensions.Options.IOptions<TelegramBotOptions> options,
        IAdminAuditService audit)
    {
        _uow = uow;
        _options = options.Value;
        _audit = audit;
    }

    public async Task<ApiResponse<TelegramConnectionLinkDto>> CreateLinkAsync(int adminUserId, CancellationToken ct = default)
    {
        var username = (_options.BotUsername ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(username)) return Invalid<TelegramConnectionLinkDto>("Telegram bot username has not been configured on the API server.");
        if (!await _uow.Repository<AdminUser>().AnyAsync(x => x.Id == adminUserId && x.IsActive, ct)) return Invalid<TelegramConnectionLinkDto>("The selected admin account is not active.");

        var now = DateTime.UtcNow;
        var previousPendingTokens = await _uow.Repository<AdminTelegramConnectionToken>()
            .ListNoTrackingAsync(x => x.AdminUserId == adminUserId && !x.RedeemedAt.HasValue && x.ExpiresAt > now, ct);
        foreach (var previous in previousPendingTokens)
        {
            previous.ExpiresAt = now;
            _uow.Repository<AdminTelegramConnectionToken>().Update(previous);
        }
        if (previousPendingTokens.Count > 0) await _uow.SaveChangesAsync(ct);

        var rawToken = Base64Url(RandomNumberGenerator.GetBytes(32));
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
        // Include the persisted record ID so redemption performs an exact
        // integer lookup, then verifies the random secret in constant time.
        // The compact base-36 ID keeps Telegram's payload within 64 chars.
        var issuedToken = $"1{ToBase36(token.Id)}_{rawToken}";
        var payload = scope == "test" ? $"connect_test_{issuedToken}" : $"connect_{issuedToken}";
        return ApiResponse<TelegramConnectionLinkDto>.SuccessResponse(new TelegramConnectionLinkDto(
            $"https://t.me/{username}?start={payload}", token.ExpiresAt, adminUserId, "valid"));
    }

    public async Task<ApiResponse<TelegramConnectionStatusDto>> GetStatusAsync(
        int adminUserId,
        CancellationToken ct = default)
    {
        var account = await _uow.Repository<AdminUser>()
            .FirstOrDefaultNoTrackingAsync(x => x.Id == adminUserId && x.IsActive, ct);
        if (account is null)
            return Invalid<TelegramConnectionStatusDto>("The selected admin account is not active.");

        var latest = (await _uow.Repository<AdminTelegramConnectionToken>()
                .ListNoTrackingAsync(x => x.AdminUserId == adminUserId, ct))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        var state = latest switch
        {
            null when account.TelegramChatId.HasValue => "linked",
            null => "none",
            { RedeemedAt: not null } => "redeemed",
            _ when latest.ExpiresAt > now => "valid",
            _ => "expired"
        };
        return ApiResponse<TelegramConnectionStatusDto>.SuccessResponse(new TelegramConnectionStatusDto(
            account.Id,
            account.TelegramChatId.HasValue,
            account.TelegramChatId,
            state,
            latest?.ExpiresAt));
    }

    public async Task<ApiResponse<NoContentDto>> RedeemAsync(TelegramConnectionRedeemRequest request, CancellationToken ct = default)
    {
        if (request is null || request.ChatId == 0 || string.IsNullOrWhiteSpace(request.Token))
            return Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_INVALID_FORMAT, "The connection link is incomplete. Open a newly generated link directly in Telegram.");
        var suppliedToken = request.Token.Trim();
        AdminTelegramConnectionToken? token = null;

        if (suppliedToken.StartsWith('1') && suppliedToken.Contains('_'))
        {
            if (!TryParseVersionedToken(suppliedToken, out var tokenId, out var rawSecret))
                return Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_INVALID_FORMAT, "The connection link format is invalid. Generate a fresh link in Volt.");

            var candidate = await _uow.Repository<AdminTelegramConnectionToken>().GetByIdAsync(tokenId, ct);
            if (candidate is null)
                return Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_NOT_FOUND, "This connection link was not found in the production Volt account. It may be a test link or it was replaced by a newer link.");
            if (!TokenMatches(candidate.TokenHash, rawSecret))
                return Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_SECRET_MISMATCH, "This connection link no longer matches its secure code. Generate a fresh link in Volt.");
            token = candidate;
        }
        else
        {
            // Keep already-issued legacy links usable during the rollout
            // window. New links always use the exact record-ID lookup above.
            byte[] legacyHash;
            try { legacyHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedToken)); }
            catch { return Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_INVALID_FORMAT, "The connection link format is invalid."); }
            token = await _uow.Repository<AdminTelegramConnectionToken>()
                .FirstOrDefaultAsync(x => x.TokenHash == legacyHash, ct);
        }

        if (token is null)
            return Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_NOT_FOUND, "This connection link was not found. Generate a fresh link in Volt.");
        var account = await _uow.Repository<AdminUser>().FirstOrDefaultAsync(x => x.Id == token.AdminUserId && x.IsActive, ct);
        if (account is null)
            return Invalid<NoContentDto>(ErrorCode.TELEGRAM_ACCOUNT_INACTIVE, "The Volt admin account for this link is inactive or no longer exists.");

        if (token.RedeemedAt.HasValue)
        {
            return token.RedeemedChatId == request.ChatId && account.TelegramChatId == request.ChatId
                ? ApiResponse<NoContentDto>.SuccessResponse(new NoContentDto())
                : Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_REDEEMED, "This connection link has already been used by a different Telegram account.");
        }

        if (token.ExpiresAt <= DateTime.UtcNow)
            return Invalid<NoContentDto>(ErrorCode.TELEGRAM_LINK_EXPIRED, "This connection link has expired. Generate a fresh link in Volt.");

        var linkedElsewhere = await _uow.Repository<AdminUser>().AnyAsync(x => x.Id != account.Id && x.TelegramChatId == request.ChatId, ct);
        if (linkedElsewhere)
            return Invalid<NoContentDto>(ErrorCode.TELEGRAM_CHAT_ALREADY_LINKED, "This Telegram account is already linked to another Volt user. Ask a full admin to clear or reassign it first.");

        account.TelegramChatId = request.ChatId;
        token.RedeemedAt = DateTime.UtcNow;
        token.RedeemedChatId = request.ChatId;
        _uow.Repository<AdminUser>().Update(account);
        _uow.Repository<AdminTelegramConnectionToken>().Update(token);
        await _uow.SaveChangesAsync(ct);
        return ApiResponse<NoContentDto>.SuccessResponse(new NoContentDto());
    }

    public async Task<ApiResponse<TelegramAnalyticsSubscriptionStatusDto>> SetAnalyticsSubscriptionAsync(
        TelegramAnalyticsSubscriptionRequest request,
        CancellationToken ct = default)
    {
        if (request is null || request.ChatId <= 0)
            return Invalid<TelegramAnalyticsSubscriptionStatusDto>("A valid Telegram chat ID is required.");

        var account = await _uow.Repository<AdminUser>()
            .FirstOrDefaultAsync(x => x.TelegramChatId == request.ChatId && x.IsActive, ct);
        if (account is null)
            return Invalid<TelegramAnalyticsSubscriptionStatusDto>("This Telegram chat is not linked to an active Volt admin account.");

        switch (NormalizeTopic(request.Topic))
        {
            case "yoxla":
                account.ReceivesYoxlaNotifications = request.Enabled;
                break;
            case "qiymetlendirme":
                account.ReceivesQiymetlendirmeNotifications = request.Enabled;
                break;
            default:
                return Invalid<TelegramAnalyticsSubscriptionStatusDto>("The notification topic must be yoxla or qiymetlendirme.");
        }

        _uow.Repository<AdminUser>().Update(account);
        await _uow.SaveChangesAsync(ct);
        return ApiResponse<TelegramAnalyticsSubscriptionStatusDto>.SuccessResponse(ToStatus(account));
    }

    public async Task<ApiResponse<TelegramAnalyticsSubscriptionStatusDto>> GetAnalyticsSubscriptionsAsync(
        long chatId,
        CancellationToken ct = default)
    {
        if (chatId <= 0)
            return Invalid<TelegramAnalyticsSubscriptionStatusDto>("A valid Telegram chat ID is required.");

        var account = await _uow.Repository<AdminUser>()
            .FirstOrDefaultAsync(x => x.TelegramChatId == chatId && x.IsActive, ct);
        return account is null
            ? Invalid<TelegramAnalyticsSubscriptionStatusDto>("This Telegram chat is not linked to an active Volt admin account.")
            : ApiResponse<TelegramAnalyticsSubscriptionStatusDto>.SuccessResponse(ToStatus(account));
    }

    public async Task<ApiResponse<IReadOnlyList<TelegramManagedAdminDto>>> GetManagedUsersAsync(
        long managerChatId,
        CancellationToken ct = default)
    {
        var manager = await GetTelegramManagerAsync(managerChatId, ct);
        if (manager is null)
            return Invalid<IReadOnlyList<TelegramManagedAdminDto>>("This Telegram chat is not linked to an active Volt full admin account.");

        var users = (await _uow.Repository<AdminUser>().ListNoTrackingAsync(ct))
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.DisplayName)
            .ThenBy(x => x.Id)
            .Select(ToManagedDto)
            .ToList();
        return ApiResponse<IReadOnlyList<TelegramManagedAdminDto>>.SuccessResponse(users);
    }

    public async Task<ApiResponse<TelegramConnectionLinkDto>> CreateManagedLinkAsync(
        long managerChatId,
        int adminUserId,
        CancellationToken ct = default)
    {
        var manager = await GetTelegramManagerAsync(managerChatId, ct);
        if (manager is null)
            return Invalid<TelegramConnectionLinkDto>("This Telegram chat is not linked to an active Volt full admin account.");

        var result = await CreateLinkAsync(adminUserId, ct);
        await _audit.WriteAsync(
            manager.Id,
            null,
            "TELEGRAM_LINK_CREATED_FROM_BOT",
            "AdminUser",
            adminUserId.ToString(),
            result.Success ? "Telegram connection link created from the management bot." : "Telegram connection link creation failed from the management bot.",
            result.Success,
            ct);
        return result;
    }

    public async Task<ApiResponse<TelegramManagedAdminDto>> ClearManagedChatAsync(
        long managerChatId,
        int adminUserId,
        CancellationToken ct = default)
    {
        var manager = await GetTelegramManagerAsync(managerChatId, ct);
        if (manager is null)
            return Invalid<TelegramManagedAdminDto>("This Telegram chat is not linked to an active Volt full admin account.");

        var account = await _uow.Repository<AdminUser>().FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
        if (account is null) return Invalid<TelegramManagedAdminDto>("The selected Volt admin account was not found.");

        account.TelegramChatId = null;
        account.ReceivesYoxlaNotifications = false;
        account.ReceivesQiymetlendirmeNotifications = false;
        _uow.Repository<AdminUser>().Update(account);
        await _uow.SaveChangesAsync(ct);
        await _audit.WriteAsync(manager.Id, null, "TELEGRAM_CHAT_CLEARED_FROM_BOT", "AdminUser", account.Id.ToString(), account.Username, true, ct);
        return ApiResponse<TelegramManagedAdminDto>.SuccessResponse(ToManagedDto(account));
    }

    public async Task<ApiResponse<TelegramManagedAdminDto>> AssignManagedChatAsync(
        long managerChatId,
        int adminUserId,
        long telegramChatId,
        CancellationToken ct = default)
    {
        var manager = await GetTelegramManagerAsync(managerChatId, ct);
        if (manager is null)
            return Invalid<TelegramManagedAdminDto>("This Telegram chat is not linked to an active Volt full admin account.");
        if (telegramChatId <= 0)
            return Invalid<TelegramManagedAdminDto>("A valid private Telegram chat ID is required.");

        var account = await _uow.Repository<AdminUser>().FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
        if (account is null) return Invalid<TelegramManagedAdminDto>("The selected Volt admin account was not found.");
        if (!account.IsActive) return Invalid<TelegramManagedAdminDto>("The selected Volt admin account is inactive.");

        var existing = await _uow.Repository<AdminUser>()
            .FirstOrDefaultNoTrackingAsync(x => x.Id != adminUserId && x.TelegramChatId == telegramChatId, ct);
        if (existing is not null)
            return Invalid<TelegramManagedAdminDto>($"This Telegram chat is already linked to {existing.DisplayName} (@{existing.Username}). Clear that connection first.");

        account.TelegramChatId = telegramChatId;
        _uow.Repository<AdminUser>().Update(account);
        await _uow.SaveChangesAsync(ct);
        await _audit.WriteAsync(manager.Id, null, "TELEGRAM_CHAT_ASSIGNED_FROM_BOT", "AdminUser", account.Id.ToString(), account.Username, true, ct);
        return ApiResponse<TelegramManagedAdminDto>.SuccessResponse(ToManagedDto(account));
    }

    public async Task<ApiResponse<TelegramManagedAdminDto>> SetManagedSubscriptionAsync(
        long managerChatId,
        int adminUserId,
        string topic,
        bool enabled,
        CancellationToken ct = default)
    {
        var manager = await GetTelegramManagerAsync(managerChatId, ct);
        if (manager is null)
            return Invalid<TelegramManagedAdminDto>("This Telegram chat is not linked to an active Volt full admin account.");

        var account = await _uow.Repository<AdminUser>().FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
        if (account is null) return Invalid<TelegramManagedAdminDto>("The selected Volt admin account was not found.");
        if (!account.IsActive) return Invalid<TelegramManagedAdminDto>("The selected Volt admin account is inactive.");

        var normalizedTopic = NormalizeTopic(topic);
        if (normalizedTopic == "yoxla") account.ReceivesYoxlaNotifications = enabled;
        else if (normalizedTopic == "qiymetlendirme") account.ReceivesQiymetlendirmeNotifications = enabled;
        else return Invalid<TelegramManagedAdminDto>("The notification topic must be yoxla or qiymetlendirme.");

        _uow.Repository<AdminUser>().Update(account);
        await _uow.SaveChangesAsync(ct);
        await _audit.WriteAsync(
            manager.Id,
            null,
            "TELEGRAM_NOTIFICATION_UPDATED_FROM_BOT",
            "AdminUser",
            account.Id.ToString(),
            $"{normalizedTopic}: {(enabled ? "enabled" : "disabled")}",
            true,
            ct);
        return ApiResponse<TelegramManagedAdminDto>.SuccessResponse(ToManagedDto(account));
    }

    private Task<AdminUser?> GetTelegramManagerAsync(long managerChatId, CancellationToken ct)
        => managerChatId <= 0
            ? Task.FromResult<AdminUser?>(null)
            : _uow.Repository<AdminUser>().FirstOrDefaultAsync(
                x => x.IsActive
                    && x.TelegramChatId == managerChatId
                    && (x.IsSuperAdmin || x.Username == "admin"),
                ct);

    private static TelegramManagedAdminDto ToManagedDto(AdminUser account)
        => new(
            account.Id,
            account.Username,
            string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName,
            account.IsActive,
            account.IsEffectiveSuperAdmin,
            account.TelegramChatId,
            account.ReceivesYoxlaNotifications,
            account.ReceivesQiymetlendirmeNotifications);

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool TokenMatches(byte[] expectedHash, string suppliedSecret)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedSecret));
        return expectedHash.Length == suppliedHash.Length
            && CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    private static string ToBase36(int value)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value <= 0) throw new InvalidOperationException("Telegram connection token ID was not generated.");
        Span<char> buffer = stackalloc char[7];
        var position = buffer.Length;
        var remaining = value;
        while (remaining > 0)
        {
            buffer[--position] = alphabet[remaining % 36];
            remaining /= 36;
        }
        return new string(buffer[position..]);
    }

    private static bool TryParseVersionedToken(string value, out int tokenId, out string rawSecret)
    {
        tokenId = 0;
        rawSecret = string.Empty;
        var separator = value.IndexOf('_');
        if (separator < 2 || value[0] != '1' || separator == value.Length - 1)
            return false;

        try
        {
            var parsed = 0;
            foreach (var character in value.AsSpan(1, separator - 1))
            {
                var normalized = char.ToLowerInvariant(character);
                var digit = normalized is >= '0' and <= '9'
                    ? normalized - '0'
                    : normalized is >= 'a' and <= 'z'
                        ? normalized - 'a' + 10
                        : -1;
                if (digit < 0 || digit >= 36) return false;
                parsed = checked(parsed * 36 + digit);
            }
            if (parsed <= 0) return false;
            tokenId = parsed;
            rawSecret = value[(separator + 1)..];
            return rawSecret.Length >= 32;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string NormalizeTopic(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("ə", "e", StringComparison.Ordinal)
            .Replace("ğ", "g", StringComparison.Ordinal)
            .Replace("ı", "i", StringComparison.Ordinal)
            .Replace("ş", "s", StringComparison.Ordinal)
            .Replace("ç", "c", StringComparison.Ordinal)
            .Replace("ö", "o", StringComparison.Ordinal)
            .Replace("ü", "u", StringComparison.Ordinal);
    private static TelegramAnalyticsSubscriptionStatusDto ToStatus(AdminUser account)
        => new(account.ReceivesYoxlaNotifications, account.ReceivesQiymetlendirmeNotifications);
    private static ApiResponse<T> Invalid<T>(string code, string message) => ApiResponse<T>.ErrorResponse(code, message);
    private static ApiResponse<T> Invalid<T>(string message) => Invalid<T>(ErrorCode.VALIDATION_ERROR, message);
}
