#nullable enable

namespace Volt.Application.Configuration
{
    /// <summary>Secrets must be supplied through protected server configuration or environment variables.</summary>
    public sealed class MetaInboxOptions
    {
        public bool Enabled { get; set; }
        public string AppId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public string VerifyToken { get; set; } = string.Empty;
        public string PageAccessToken { get; set; } = string.Empty;
        public string WhatsAppAccessToken { get; set; } = string.Empty;
        public string WhatsAppPhoneNumberId { get; set; } = string.Empty;
        public string WhatsAppBusinessAccountId { get; set; } = string.Empty;
        public string WhatsAppEmbeddedSignupConfigurationId { get; set; } = string.Empty;
        public string WhatsAppOAuthRedirectUri { get; set; } = string.Empty;
        public string GraphApiVersion { get; set; } = "v26.0";
    }
}
