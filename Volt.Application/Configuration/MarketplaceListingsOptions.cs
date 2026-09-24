namespace Volt.Application.Configuration
{
    // Shared settings for AI-prepared marketplace listings (Lalafo, Tap.az).
    public sealed class MarketplaceListingsOptions
    {
        // Empty falls back to ProductAiImport's ApiKey/Model, same as ContentAiGeneration does.
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string ReasoningEffort { get; set; } = "low";
        public int RequestTimeoutSeconds { get; set; } = 120;
        public int MaxOutputTokens { get; set; } = 6000;
        public int PayloadLifetimeMinutes { get; set; } = 30;
        public int MaxImages { get; set; } = 10;
    }
}
