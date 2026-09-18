namespace Volt.Application.Configuration
{
    public sealed class ContentAiOptions
    {
        public bool Enabled { get; set; }
        // Blank falls back to ProductAiImportOptions.ApiKey -- same OpenAI account, one key by default.
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-5.6";
        public int DraftLifetimeHours { get; set; } = 168;
        public int RequestTimeoutSeconds { get; set; } = 180;
        public int MaxConcurrentJobs { get; set; } = 2;
        public string ReasoningEffort { get; set; } = "low";
        public int MaxOutputTokens { get; set; } = 16000;
        public int MinTopicLength { get; set; } = 10;
        public int MaxTopicLength { get; set; } = 400;
        public int MaxAngleNotesLength { get; set; } = 800;
    }
}
