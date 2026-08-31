namespace Volt.Application.Configuration
{
    public sealed class ProductAiImportOptions
    {
        public bool Enabled { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-5.6";
        public string TrustedAssetBaseUrl { get; set; } = "https://cloudfiles.volt.az/";
        public int MaxFiles { get; set; } = 10;
        public long MaxCombinedBytes { get; set; } = 50L * 1024 * 1024;
        public int DraftLifetimeHours { get; set; } = 168;
        public int RequestTimeoutSeconds { get; set; } = 180;
        public int MaxConcurrentJobs { get; set; } = 2;
        public string ReasoningEffort { get; set; } = "low";
        public int MaxOutputTokens { get; set; } = 16000;
    }
}
