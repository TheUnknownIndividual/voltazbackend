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
        public int PayloadLifetimeMinutes { get; set; } = 60;
        public int MaxBatchSize { get; set; } = 12;
        public int MaxParallelAiCalls { get; set; } = 3;

        // Lines appended to every description, after the AI text and any category notes.
        public List<string> DescriptionFooter { get; set; } = new();
        // Reference descriptions the AI imitates for structure, tone and formatting.
        public List<string> DescriptionExamples { get; set; } = new();
    }
}
