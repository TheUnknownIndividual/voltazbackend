namespace Volt.Application.Configuration;

/// <summary>Bind only from protected environment/server configuration, never source control.</summary>
public sealed class TelegramBotOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
    public string LinkApiKey { get; set; } = string.Empty;
    public string ConnectionLinkScope { get; set; } = "prod";
}
