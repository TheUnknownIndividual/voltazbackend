namespace Volt.Application.Dtos.Admin;

public sealed record TelegramConnectionLinkDto(string Url, DateTime ExpiresAt);
public sealed record TelegramConnectionRedeemRequest(string Token, long ChatId);
